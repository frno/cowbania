using Cowbania.Host.Presentation.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Cowbania.Host.Presentation.Hud;

internal sealed class HudRenderer(RenderContext context)
{
    public void Draw(GameWorld world)
    {
        Panel(new Rectangle(10, 10, 176, 94));
        for (var i = 0; i < GameWorld.MaximumHealth; i++)
            context.Sprite(context.Assets.Ui[i < world.Health ? "heart_full" : "heart_empty"], new Rectangle(20 + i * 38, 18, 32, 32), Color.White);
        for (var i = 0; i < 6; i++)
            context.Sprite(context.Assets.Ui[i < world.Ammo ? "ammo_full" : "ammo_empty"], new Rectangle(20 + i * 26, 56, 24, 24), Color.White);
        if (world.IsReloading)
            context.Rect(new Rectangle(20, 84, 146, 4), new Color(239, 190, 95));
        var width = context.GraphicsDevice.Viewport.Width;
        var height = context.GraphicsDevice.Viewport.Height;
        DrawScore(world, width);
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

    private void DrawScore(GameWorld world, int viewportWidth)
    {
        var panel = new Rectangle(viewportWidth / 2 - 140, 10, 280, 78);
        Panel(panel);
        var bone = new Color(224, 204, 157);
        var gold = new Color(239, 190, 95);

        Text("SCORE", panel.X + 20, panel.Y + 14, bone, 2);
        Number(world.Score, panel.X + 96, panel.Y + 14, gold, 3);

        context.Sprite(context.Assets.Ui["coin"], new Rectangle(panel.X + 20, panel.Y + 42, 24, 24), Color.White);
        Number(world.CurrentRoomCoinsCollected, panel.X + 56, panel.Y + 45, bone, 3);
        Text("/", panel.X + 104, panel.Y + 45, gold, 3);
        Number(world.CurrentRoomTotalCoins, panel.X + 124, panel.Y + 45, bone, 3);
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

    private void Number(int value, int x, int y, Color color, int scale = 3)
    {
        foreach (var digit in Math.Max(0, value).ToString())
        {
            Digit(digit - '0', x, y, color, scale);
            x += scale * 4;
        }
    }

    private void Text(string value, int x, int y, Color color, int scale)
    {
        foreach (var character in value)
        {
            Glyph(character, x, y, color, scale);
            x += scale * 4;
        }
    }

    private void Glyph(char character, int x, int y, Color color, int scale)
    {
        ReadOnlySpan<byte> rows = character switch
        {
            'S' => [0b111, 0b100, 0b111, 0b001, 0b111],
            'C' => [0b111, 0b100, 0b100, 0b100, 0b111],
            'O' => [0b111, 0b101, 0b101, 0b101, 0b111],
            'R' => [0b110, 0b101, 0b110, 0b101, 0b101],
            'E' => [0b111, 0b100, 0b110, 0b100, 0b111],
            '/' => [0b001, 0b001, 0b010, 0b100, 0b100],
            _ => [0, 0, 0, 0, 0]
        };
        DrawGlyph(rows, x, y, color, scale);
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
        DrawGlyph(rows, x, y, color, scale);
    }

    private void DrawGlyph(ReadOnlySpan<byte> rows, int x, int y, Color color, int scale)
    {
        for (var row = 0; row < rows.Length; row++)
        for (var column = 0; column < 3; column++)
            if ((rows[row] & (1 << (2 - column))) != 0)
                context.Rect(new Rectangle(x + column * scale, y + row * scale, scale, scale), color);
    }
}
