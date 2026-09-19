using Cowbania.Host.Presentation.Assets;
using Cowbania.Host.Presentation.Camera;
using Cowbania.Host.Presentation.Effects;
using Cowbania.Host.Presentation.Hud;
using Microsoft.Xna.Framework.Graphics;

namespace Cowbania.Host.Presentation.Rendering;

internal sealed class GameRenderer
{
    private readonly GraphicsDevice graphicsDevice;
    private readonly GameCamera camera;
    private readonly RenderContext context;
    private readonly StageRenderer stage;
    private readonly ActorRenderer actors;
    private readonly ProjectileRenderer projectiles;
    private readonly HudRenderer hud;

    public GameRenderer(
        GraphicsDevice graphicsDevice,
        SpriteBatch spriteBatch,
        Texture2D pixel,
        FrontierAssets assets,
        PresentationTimeline timeline,
        GameCamera camera)
    {
        this.graphicsDevice = graphicsDevice;
        this.camera = camera;
        context = new RenderContext(graphicsDevice, spriteBatch, pixel, assets, camera);
        stage = new StageRenderer(context);
        actors = new ActorRenderer(context, timeline, new EffectRenderer(context, timeline));
        projectiles = new ProjectileRenderer(context);
        hud = new HudRenderer(context);
    }

    public void Draw(GameWorld world)
    {
        var room = world.CurrentRoom;
        camera.Follow(room, world.PlayerPosition, graphicsDevice.Viewport.Width);
        var palette = StagePalette.For(room.Id);
        graphicsDevice.Clear(palette.SkyTop);
        context.Begin();
        stage.DrawBackdrop(room, palette);
        stage.DrawSetDressing(room);
        foreach (var solid in room.Solids)
            stage.DrawSolid(solid, room);
        stage.DrawLandmarks(room);
        actors.DrawPlayer(world);
        actors.DrawEnemies(world);
        foreach (var projectile in world.Projectiles)
            projectiles.Draw(projectile);
        actors.DrawPickups(world);
        stage.DrawForeground(room, palette);
        hud.Draw(world);
        context.End();
    }
}
