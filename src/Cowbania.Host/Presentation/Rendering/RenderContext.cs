using Cowbania.Host.Presentation.Assets;
using Cowbania.Host.Presentation.Camera;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NumericsVector2 = System.Numerics.Vector2;

namespace Cowbania.Host.Presentation.Rendering;

internal sealed class RenderContext(
    GraphicsDevice graphicsDevice,
    SpriteBatch spriteBatch,
    Texture2D pixel,
    FrontierAssets assets,
    GameCamera camera)
{
    public GraphicsDevice GraphicsDevice { get; } = graphicsDevice;
    public SpriteBatch SpriteBatch { get; } = spriteBatch;
    public Texture2D Pixel { get; } = pixel;
    public FrontierAssets Assets { get; } = assets;
    public GameCamera Camera { get; } = camera;

    public void Begin(RasterizerState? rasterizerState = null) =>
        SpriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: rasterizerState);

    public void End() => SpriteBatch.End();
    public void Rect(Rectangle rectangle, Color color) => SpriteBatch.Draw(Pixel, rectangle, color);
    public void Sprite(Texture2D texture, Rectangle destination, Color color) =>
        SpriteBatch.Draw(texture, destination, color);
    public void Sprite(Texture2D texture, Rectangle destination, Color color, SpriteEffects effects) =>
        SpriteBatch.Draw(texture, destination, null, color, 0, Vector2.Zero, effects, 0);

    public void Anchored(
        Texture2D texture,
        NumericsVector2 anchor,
        NumericsVector2 sourceAnchor,
        float scale,
        int facingDirection = 1,
        Color? tint = null)
    {
        var effects = facingDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        SpriteBatch.Draw(
            texture,
            Camera.ToScreen(anchor),
            null,
            tint ?? Color.White,
            0,
            new Vector2(sourceAnchor.X, sourceAnchor.Y),
            scale,
            effects,
            0);
    }
}
