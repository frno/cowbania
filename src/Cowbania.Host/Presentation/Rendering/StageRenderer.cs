using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NumericsVector2 = System.Numerics.Vector2;

namespace Cowbania.Host.Presentation.Rendering;

internal sealed class StageRenderer(RenderContext context)
{
    private readonly RasterizerState scissorRasterizer = new() { ScissorTestEnable = true, CullMode = CullMode.None };

    public void DrawBackdrop(RoomDefinition room, StagePalette palette)
    {
        context.Rect(context.Camera.ToScreen(room.Bounds), palette.SkyBottom);
        var prefix = room.Id == RoomCatalog.Branch.Id ? "branch" : "hub";
        DrawBackgroundBand(context.Assets.Backgrounds[$"{prefix}_far"], 0.08f);
        DrawBackgroundBand(context.Assets.Backgrounds[$"{prefix}_mid"], 0.18f);
    }

    public void DrawSetDressing(RoomDefinition room)
    {
        var tint = new Color(145, 137, 140, 190);
        if (room.Id == RoomCatalog.Hub.Id)
        {
            Prop("sign", new NumericsVector2(room.Bounds.X + 360, room.Ground.Y), 3, tint: tint);
            Prop("cactus_0", new NumericsVector2(room.Bounds.X + 820, room.Ground.Y), 3, tint: tint);
            Prop("cactus_1", new NumericsVector2(room.Bounds.X + 1320, room.Ground.Y), 3, tint: tint);
            Prop("crate", new NumericsVector2(room.Bounds.X + 610, room.Ground.Y), 2, tint: tint);
            Prop("wagon_debris", new NumericsVector2(room.Bounds.X + 1580, room.Ground.Y), 3, tint: tint);
            Prop("cactus_0", new NumericsVector2(room.Bounds.X + 2140, room.Ground.Y), 3, tint: tint);
            Prop("mine_timber", new NumericsVector2(room.Bounds.X + 2700, room.Ground.Y), 3, tint: tint);
            Prop("cactus_1", new NumericsVector2(room.Bounds.X + 3540, room.Ground.Y), 3, tint: tint);
            Prop("wagon_debris", new NumericsVector2(room.Bounds.X + 4020, room.Ground.Y), 3, tint: tint);
            Prop("sign", new NumericsVector2(room.Bounds.X + 4740, room.Ground.Y), 3, flip: true, tint: tint);
        }
        else
        {
            Prop("mine_timber", new NumericsVector2(room.Bounds.X + 520, room.Ground.Y), 3, tint: tint);
            Prop("mine_timber", new NumericsVector2(room.Bounds.X + 1220, room.Ground.Y), 3, tint: tint);
            Prop("wagon_debris", new NumericsVector2(room.Bounds.X + 980, room.Ground.Y), 3, tint: tint);
            Prop("cactus_0", new NumericsVector2(room.Bounds.X + 280, room.Ground.Y), 3, tint: tint);
            Prop("cactus_1", new NumericsVector2(room.Bounds.X + 1880, room.Ground.Y), 3, tint: tint);
            Prop("wagon_debris", new NumericsVector2(room.Bounds.X + 2520, room.Ground.Y), 3, tint: tint);
            Prop("sign", new NumericsVector2(room.Bounds.X + 3100, room.Ground.Y), 3, tint: tint);
            Prop("mine_timber", new NumericsVector2(room.Bounds.X + 3880, room.Ground.Y), 3, tint: tint);
            Prop("crate", new NumericsVector2(room.Bounds.X + 4260, room.Ground.Y), 2, tint: tint);
            Prop("mine_timber", new NumericsVector2(room.Bounds.X + 5160, room.Ground.Y), 3, tint: tint);
            Prop("cactus_0", new NumericsVector2(room.Bounds.X + 5540, room.Ground.Y), 3, tint: tint);
            Prop("sign", new NumericsVector2(room.Bounds.X + 6080, room.Ground.Y), 3, flip: true, tint: tint);
        }
    }

    public void DrawSolid(RoomRect solid, RoomDefinition room)
    {
        var rectangle = context.Camera.ToScreen(solid);
        var isGround = solid.Height > 24;
        context.Rect(rectangle, new Color(35, 24, 32));
        var viewport = new Rectangle(0, 0, context.GraphicsDevice.Viewport.Width, context.GraphicsDevice.Viewport.Height);
        var clip = Rectangle.Intersect(rectangle, viewport);
        if (clip.Width <= 0 || clip.Height <= 0)
            return;

        context.End();
        context.GraphicsDevice.ScissorRectangle = clip;
        context.Begin(scissorRasterizer);
        var body = context.Assets.Terrain[isGround ? "ground_body" : "stone"];
        foreach (var tile in TerrainTileLayout.Cover(rectangle))
            context.SpriteBatch.Draw(body, tile.Destination, tile.Source, Color.White);
        if (isGround)
            TileHorizontal(context.Assets.Terrain["ground_cap"], rectangle.X, rectangle.Y, rectangle.Width);
        else
        {
            TerrainTile(context.Assets.Terrain["platform_left"], rectangle.X, rectangle.Y);
            if (rectangle.Width > 96)
                TileHorizontal(context.Assets.Terrain["platform_middle"], rectangle.X + 48, rectangle.Y, rectangle.Width - 96);
            TerrainTile(context.Assets.Terrain["platform_right"], Math.Max(rectangle.X, rectangle.Right - 48), rectangle.Y);
        }
        context.End();
        context.GraphicsDevice.ScissorRectangle = viewport;
        context.Begin();
        context.Rect(new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, Math.Min(3, rectangle.Height)), new Color(239, 190, 95));
    }

    public void DrawLandmarks(RoomDefinition room)
    {
        Prop("transition_gate", new NumericsVector2(room.Bounds.X + 24, room.Ground.Y), 3);
        Prop("transition_gate", new NumericsVector2(room.Bounds.Right - 24, room.Ground.Y), 3, flip: true);
        Prop("checkpoint", room.Checkpoint, 3);
        Prop("shortcut", room.Shortcut, 3);
    }

    public void DrawForeground(RoomDefinition room, StagePalette palette)
    {
        foreach (var surface in room.Solids.Where(solid => solid.Height > 24))
        {
            var ground = context.Camera.ToScreen(surface);
            for (var x = ground.X + 6; x < ground.Right; x += 48)
            {
                var blade = ((x / 48) & 1) == 0 ? 6 : 10;
                context.Rect(new Rectangle(x, ground.Y - blade, 3, blade), palette.GroundTop);
                context.Rect(new Rectangle(x + 6, ground.Y - Math.Max(4, blade - 3), 3, Math.Max(4, blade - 3)), palette.GroundTop);
            }
        }
    }

    private void DrawBackgroundBand(Texture2D texture, float parallax)
    {
        const int scale = 4;
        var width = texture.Width * scale;
        var start = -(int)(context.Camera.X * parallax) % width;
        if (start > 0) start -= width;
        for (var x = start; x < context.GraphicsDevice.Viewport.Width; x += width)
            context.Sprite(texture, new Rectangle(x, 0, width, texture.Height * scale), Color.White);
    }

    private void Prop(string name, NumericsVector2 anchor, float scale, bool flip = false, Color? tint = null)
    {
        var texture = context.Assets.Props[name];
        context.Anchored(texture, anchor, new NumericsVector2(texture.Width / 2f, texture.Height), scale, flip ? -1 : 1, tint);
    }

    private void TileHorizontal(Texture2D texture, int x, int y, int width)
    {
        for (var offset = 0; offset < width; offset += 48)
            TerrainTile(texture, x + offset, y);
    }

    private void TerrainTile(Texture2D texture, int x, int y) =>
        context.SpriteBatch.Draw(texture, new Rectangle(x, y, 48, 48), new Rectangle(0, 0, 16, 16), Color.White);
}

internal readonly record struct TerrainTileDraw(Rectangle Destination, Rectangle Source);

internal static class TerrainTileLayout
{
    private const int TileSize = 48;

    public static IEnumerable<TerrainTileDraw> Cover(Rectangle clip)
    {
        for (var y = clip.Y; y < clip.Bottom; y += TileSize)
        for (var x = clip.X; x < clip.Right; x += TileSize)
            yield return new TerrainTileDraw(new Rectangle(x, y, TileSize, TileSize), new Rectangle(0, 0, 16, 16));
    }
}
