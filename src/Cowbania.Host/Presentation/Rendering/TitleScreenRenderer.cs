using Microsoft.Xna.Framework;

namespace Cowbania.Host.Presentation.Rendering;

internal sealed class TitleScreenRenderer(RenderContext context, TitleFilmPlayer? film)
{
    public void Draw()
    {
        var viewport = context.GraphicsDevice.Viewport;
        context.GraphicsDevice.Clear(Color.Black);
        context.Begin();
        if (film?.CurrentFrame is { } frame)
            context.Sprite(frame, Cover(frame.Width, frame.Height, viewport.Width, viewport.Height), Color.White * 0.46f);

        var logo = context.Assets.TitleLogo;
        const int logoWidth = 640;
        const int logoHeight = 256;
        context.Sprite(
            logo,
            new Rectangle(viewport.Width / 2 - logoWidth / 2, viewport.Height / 2 - logoHeight / 2, logoWidth, logoHeight),
            Color.White);
        context.End();
    }

    private static Rectangle Cover(int sourceWidth, int sourceHeight, int viewportWidth, int viewportHeight)
    {
        var scale = Math.Max((float)viewportWidth / sourceWidth, (float)viewportHeight / sourceHeight);
        var width = (int)MathF.Ceiling(sourceWidth * scale);
        var height = (int)MathF.Ceiling(sourceHeight * scale);
        return new Rectangle((viewportWidth - width) / 2, (viewportHeight - height) / 2, width, height);
    }
}