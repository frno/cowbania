using Cowbania.Host.Presentation.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NumericsVector2 = System.Numerics.Vector2;

namespace Cowbania.Host.Presentation.Rendering;

internal sealed class ActorRenderer(
    RenderContext context,
    PresentationTimeline timeline,
    EffectRenderer effects)
{
    private const int PlayerScale = 2;
    private const int ActorScale = 3;
    private const int PickupScale = 2;

    public void DrawPlayer(GameWorld world)
    {
        DrawActor(
            context.Assets.Player[timeline.PlayerFrame().AssetKey],
            world.PlayerPosition,
            FrontierAnimationCatalog.PlayerMetadata,
            PlayerScale,
            world.FacingDirection);
        effects.DrawPlayer(world);
    }

    public void DrawEnemies(GameWorld world)
    {
        foreach (var enemy in world.Enemies)
        {
            var presentation = EnemyPresentationStateSelector.Select(enemy);
            DrawTelegraph(enemy, presentation);
            var clip = FrontierAnimationCatalog.ForEnemy(enemy);
            var cache = enemy.Archetype == EnemyArchetype.Bandit
                ? context.Assets.Bandit
                : context.Assets.Wildlife;
            var metadata = enemy.Archetype == EnemyArchetype.Bandit
                ? FrontierAnimationCatalog.BanditMetadata
                : FrontierAnimationCatalog.WildlifeMetadata;
            var frame = Math.Clamp(timeline.EnemyFrame(enemy.Id), 0, clip.Frames.Length - 1);
            DrawActor(cache[clip.Frames[frame].AssetKey], enemy.Position, metadata, ActorScale, presentation.FacingDirection);
            effects.DrawEnemy(enemy, presentation);
        }
    }

    public void DrawPickups(GameWorld world)
    {
        foreach (var pickup in world.AvailablePickups)
        {
            context.Anchored(
                context.Assets.Pickup[timeline.PickupFrame(pickup.Type).AssetKey],
                pickup.Position,
                FrontierAnimationCatalog.PickupMetadata.SourceFeetAnchor,
                PickupScale);
        }
        effects.DrawPickup();
    }

    private void DrawActor(
        Texture2D texture,
        NumericsVector2 position,
        AnimationActorMetadata metadata,
        int scale,
        int facing) =>
        context.Anchored(texture, position, metadata.SourceFeetAnchor, scale, facing);

    private void DrawTelegraph(EnemyState enemy, EnemyPresentationDefinition presentation)
    {
        if (presentation.TelegraphMarker == EnemyTelegraphMarker.None)
            return;
        var position = context.Camera.ToScreen(enemy.Position);
        var x = (int)position.X;
        var feetY = (int)position.Y;
        var facing = presentation.FacingDirection;
        var progress = Math.Clamp(enemy.AttackTimerNormalized, 0, 1);
        switch (presentation.TelegraphMarker)
        {
            case EnemyTelegraphMarker.NoticeBurst:
                context.Rect(new Rectangle(x - 2, feetY - 66, 4, 14), Color.White);
                context.Rect(new Rectangle(x - 12, feetY - 62, 7, 4), Color.Gold);
                context.Rect(new Rectangle(x + 5, feetY - 62, 7, 4), Color.Gold);
                break;
            case EnemyTelegraphMarker.BanditAimLine:
                var aimLength = 40 + (int)(72 * progress);
                var aimStart = x + facing * 18;
                context.Rect(
                    new Rectangle(facing > 0 ? aimStart : aimStart - aimLength, feetY - 27, aimLength, 2),
                    new Color(255, 196, 72, 190));
                context.Rect(new Rectangle(aimStart + facing * (aimLength - 3) - 2, feetY - 30, 5, 8), Color.Red);
                break;
            case EnemyTelegraphMarker.BanditMuzzleFlash:
                var muzzleX = x + facing * 24;
                context.Rect(new Rectangle(muzzleX - 6, feetY - 31, 12, 12), Color.Gold);
                context.Rect(new Rectangle(muzzleX - 2, feetY - 35, 4, 20), Color.White);
                break;
            case EnemyTelegraphMarker.WildlifeLungeArrow:
                var arrowX = x + facing * (28 + (int)(16 * progress));
                context.Rect(
                    new Rectangle(facing > 0 ? x + 12 : arrowX, feetY - 18, Math.Abs(arrowX - (x + facing * 12)), 4),
                    new Color(154, 255, 112, 210));
                context.Rect(new Rectangle(arrowX - 4, feetY - 24, 8, 16), Color.White);
                break;
            case EnemyTelegraphMarker.WildlifeLungeTrail:
                var trailX = facing > 0 ? x - 52 : x + 16;
                context.Rect(new Rectangle(trailX, feetY - 34, 36, 6), new Color(105, 220, 104, 150));
                context.Rect(new Rectangle(trailX - facing * 8, feetY - 22, 26, 4), new Color(180, 255, 150, 120));
                break;
        }
    }
}
