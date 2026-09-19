using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NumericsVector2 = System.Numerics.Vector2;

namespace Cowbania.Host.Presentation.Rendering;

internal sealed class ProjectileRenderer(RenderContext context)
{
    public void Draw(ProjectileState projectile)
    {
        var visual = ProjectileVisualCatalog.For(projectile.Owner);
        var direction = projectile.Velocity.LengthSquared() > 0
            ? NumericsVector2.Normalize(projectile.Velocity)
            : NumericsVector2.UnitX;
        var angle = MathF.Atan2(direction.Y, direction.X);
        var position = context.Camera.ToScreen(projectile.Position);
        context.SpriteBatch.Draw(
            context.Pixel, position, null, visual.PrimaryColor, angle,
            new Vector2(0.5f), new Vector2(visual.Length, visual.Thickness), SpriteEffects.None, 0);
        if (visual.Shape == ProjectileVisualShape.PlayerTracer)
        {
            context.SpriteBatch.Draw(
                context.Pixel, position, null, visual.SecondaryColor, angle,
                new Vector2(0.5f), new Vector2(visual.Length - 4, 1), SpriteEffects.None, 0);
            return;
        }
        var tip = position + new Vector2(direction.X, direction.Y) * (visual.Length / 2f);
        context.SpriteBatch.Draw(
            context.Pixel, tip, null, visual.SecondaryColor, angle + MathF.PI / 4,
            new Vector2(0.5f), new Vector2(5), SpriteEffects.None, 0);
    }
}

internal enum ProjectileVisualShape
{
    PlayerTracer,
    HostileBolt
}

internal readonly record struct ProjectileVisualDefinition(
    ProjectileVisualShape Shape,
    Color PrimaryColor,
    Color SecondaryColor,
    int Length,
    int Thickness);

internal static class ProjectileVisualCatalog
{
    private static readonly ProjectileVisualDefinition Player =
        new(ProjectileVisualShape.PlayerTracer, new Color(255, 211, 92), Color.White, 16, 3);
    private static readonly ProjectileVisualDefinition Hostile =
        new(ProjectileVisualShape.HostileBolt, new Color(190, 48, 64), new Color(255, 174, 92), 12, 6);

    public static ProjectileVisualDefinition For(ProjectileOwner owner) =>
        owner == ProjectileOwner.Player ? Player : Hostile;
}
