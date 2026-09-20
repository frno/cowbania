using Cowbania.Host.Presentation.Rendering;
using Microsoft.Xna.Framework;
using NumericsVector2 = System.Numerics.Vector2;

namespace Cowbania.Host.Presentation.Effects;

internal sealed class EffectRenderer(RenderContext context, PresentationTimeline timeline)
{
    private const int PlayerScale = 2;

    public void DrawPlayer(GameWorld world)
    {
        if (world.IsDashing)
            Draw("dash", timeline.FixedFrame(3, 15), world.PlayerPosition, -world.FacingDirection, 3);
        else if (timeline.HurtSeconds > 0)
            Draw("hurt", Math.Min(1, (int)((0.35f - timeline.HurtSeconds) * 8)), world.PlayerPosition, world.FacingDirection, 3);
        else if (world.IsGrounded && MathF.Abs(world.PlayerVelocity.X) > 1)
            Draw("dust", timeline.FixedFrame(3, 10), world.PlayerPosition, -world.FacingDirection, 2);

        if (timeline.ShootSeconds <= 0)
            return;
        var metadata = FrontierAnimationCatalog.PlayerMetadata;
        var position = world.PlayerPosition + new NumericsVector2(
            (metadata.SourceEffectAnchor.X - metadata.SourceFeetAnchor.X) * PlayerScale * world.FacingDirection,
            (metadata.SourceEffectAnchor.Y - metadata.SourceFeetAnchor.Y) * PlayerScale);
        Draw("muzzle", Math.Min(2, (int)((0.14f - timeline.ShootSeconds) * 21)), position, world.FacingDirection, 2);
    }

    public void DrawEnemy(EnemyState enemy, EnemyPresentationDefinition presentation)
    {
        if (!enemy.Alive)
        {
            var clock = timeline.DefeatClock(enemy.Id);
            if (clock?.IsVisible == true)
                Draw("defeat", clock.CurrentFrameIndex, enemy.Position, presentation.FacingDirection, 3);
            return;
        }
        if (!presentation.AttackActive)
            return;

        var metadata = enemy.Archetype == EnemyArchetype.Bandit
            ? FrontierAnimationCatalog.BanditMetadata
            : FrontierAnimationCatalog.WildlifeMetadata;
        var position = enemy.Position + new NumericsVector2(
            (metadata.SourceEffectAnchor.X - metadata.SourceFeetAnchor.X) * 3 * presentation.FacingDirection,
            (metadata.SourceEffectAnchor.Y - metadata.SourceFeetAnchor.Y) * 3);
        Draw(
            enemy.Archetype == EnemyArchetype.Bandit ? "muzzle" : "dash",
            Math.Min(2, (int)(Math.Clamp(enemy.AttackTimerNormalized, 0, 0.999f) * 3)),
            position,
            presentation.FacingDirection,
            2);
    }

    public void DrawPickup()
    {
        if (timeline.PickupSeconds <= 0)
            return;
        Draw("pickup", Math.Min(3, (int)((0.32f - timeline.PickupSeconds) * 12.5f)), timeline.PickupPosition, 1, 2);
    }

    private void Draw(string effect, int frame, NumericsVector2 anchor, int facing, float scale) =>
        context.Anchored(
            context.Assets.Effects[$"{effect}_{frame}"],
            anchor,
            new NumericsVector2(8, 8),
            scale,
            facing);
}
