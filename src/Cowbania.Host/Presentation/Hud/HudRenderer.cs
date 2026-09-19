using Cowbania.Host.Presentation.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Cowbania.Host.Presentation.Hud;

internal sealed class HudRenderer(RenderContext context)
{
    public void Draw(GameWorld world)
    {
        Panel(new Rectangle(10, 10, 248, 94));
        for (var i = 0; i < GameWorld.MaximumHealth; i++)
            context.Sprite(context.Assets.Ui[i < world.Health ? "heart_full" : "heart_empty"], new Rectangle(20 + i * 38, 18, 32, 32), Color.White);
        for (var i = 0; i < 6; i++)
            context.Sprite(context.Assets.Ui[i < world.Ammo ? "ammo_full" : "ammo_empty"], new Rectangle(20 + i * 26, 56, 24, 24), Color.White);
        if (world.IsReloading)
            context.Rect(new Rectangle(20, 84, 146, 4), new Color(239, 190, 95));
        context.Sprite(context.Assets.Ui["currency"], new Rectangle(184, 54, 32, 32), Color.White);
        Number(world.Currency, 220, 61, new Color(224, 204, 157));

        var width = context.GraphicsDevice.Viewport.Width;
        var height = context.GraphicsDevice.Viewport.Height;
        Panel(new Rectangle(width - 74, 10, 64, 64));
        context.Sprite(context.Assets.Ui["slot_frame"], new Rectangle(width - 66, 18, 48, 48), Color.White);
        Digit(1, width - 50, 31, new Color(239, 190, 95), 4);

        if (world.IsPaused)
        {
            context.Rect(new Rectangle(0, 0, width, height), new Color(20, 14, 20, 96));
            var panel = new Rectangle(width / 2 - 96, 202, 192, 128);
            Panel(panel);
            context.Rect(new Rectangle(panel.Center.X - 28, panel.Y + 32, 16, 64), new Color(224, 204, 157));
            context.Rect(new Rectangle(panel.Center.X + 12, panel.Y + 32, 16, 64), new Color(224, 204, 157));
        }
        if (world.Completed)
        {
            var panel = new Rectangle(width / 2 - 176, 24, 352, 72);
            Panel(panel);
            context.Rect(new Rectangle(panel.X + 32, panel.Y + 25, panel.Width - 64, 5), new Color(203, 133, 54));
            context.Rect(new Rectangle(panel.X + 54, panel.Y + 40, panel.Width - 108, 4), new Color(239, 190, 95));
        }
    }

    private void Panel(Rectangle panel)
    {
        context.Rect(panel, new Color(35, 24, 32, 220));
        context.Rect(new Rectangle(panel.X + 4, panel.Y + 4, panel.Width - 8, panel.Height - 8), new Color(57, 35, 38, 230));
        var corner = context.Assets.Ui["panel_corner"];
        context.Sprite(corner, new Rectangle(panel.X, panel.Y, 32, 32), Color.White);
        context.Sprite(corner, new Rectangle(panel.Right - 32, panel.Y, 32, 32), Color.White, SpriteEffects.FlipHorizontally);
        context.Sprite(corner, new Rectangle(panel.X, panel.Bottom - 32, 32, 32), Color.White, SpriteEffects.FlipVertically);
        context.Sprite(corner, new Rectangle(panel.Right - 32, panel.Bottom - 32, 32, 32), Color.White, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically);
    }

    private void Number(int value, int x, int y, Color color)
    {
        foreach (var digit in Math.Max(0, value).ToString())
        {
            Digit(digit - '0', x, y, color, 3);
            x += 12;
        }
    }

    private void Digit(int digit, int x, int y, Color color, int scale)
    {
        ReadOnlySpan<byte> rows = digit switch
        {
            0 => [0b111, 0b101, 0b101, 0b101, 0b111],
            1 => [0b010, 0b110, 0b010, 0b010, 0b111],
            2 => [0b111, 0b001, 0b111, 0b100, 0b111],
            3 => [0b111, 0b001, 0b111, 0b001, 0b111],
            4 => [0b101, 0b101, 0b111, 0b001, 0b001],
            5 => [0b111, 0b100, 0b111, 0b001, 0b111],
            6 => [0b111, 0b100, 0b111, 0b101, 0b111],
            7 => [0b111, 0b001, 0b010, 0b010, 0b010],
            8 => [0b111, 0b101, 0b111, 0b101, 0b111],
            9 => [0b111, 0b101, 0b111, 0b001, 0b111],
            _ => [0, 0, 0, 0, 0]
        };
        for (var row = 0; row < rows.Length; row++)
        for (var column = 0; column < 3; column++)
            if ((rows[row] & (1 << (2 - column))) != 0)
                context.Rect(new Rectangle(x + column * scale, y + row * scale, scale, scale), color);
    }
}
