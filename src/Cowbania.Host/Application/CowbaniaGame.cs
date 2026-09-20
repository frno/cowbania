using Cowbania.Host.Audio;
using Cowbania.Host.Diagnostics;
using Cowbania.Host.Input;
using Cowbania.Host.Presentation;
using Cowbania.Host.Presentation.Assets;
using Cowbania.Host.Presentation.Camera;
using Cowbania.Host.Presentation.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Cowbania.Host.Application;

internal sealed class CowbaniaGame : Game
{
    private readonly GraphicsDeviceManager graphics;
    private readonly GameWorld world = new();
    private readonly PresentationTimeline timeline = new();
    private readonly AudioInitialization audioInitialization = new();
    private readonly AudioEventBus audioBus;
    private readonly MusicPlayer titleMusicPlayer;
    private readonly MusicPlayer gameplayMusicPlayer;
    private readonly FrameTelemetry telemetry = new();
    private readonly TitleScreenState titleScreen = new();
    private GameUpdateCoordinator updateCoordinator = null!;
    private FrontierAssets assets = null!;
    private GameRenderer renderer = null!;
    private TitleFilmPlayer? titleFilm;
    private TitleScreenRenderer titleRenderer = null!;

    public CowbaniaGame()
    {
        StartupDiagnostics.Mark("CowbaniaGame constructor start");
        audioBus = new AudioEventBus(audioInitialization);
        titleMusicPlayer = new MusicPlayer(audioInitialization, "Music_Title.wav", 0.5f);
        gameplayMusicPlayer = new MusicPlayer(audioInitialization);
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1024,
            PreferredBackBufferHeight = 576,
            IsFullScreen = false,
            HardwareModeSwitch = false
        };
        Window.Title = "Cowbania";
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        Activated += (_, _) => RuntimeLog.Info("lifecycle activated");
        Deactivated += (_, _) => RuntimeLog.Info("lifecycle deactivated");
        Exiting += (_, _) => RuntimeLog.Info("lifecycle exiting");
        StartupDiagnostics.Mark("CowbaniaGame constructor complete");
    }

    protected override void LoadContent()
    {
        StartupDiagnostics.Mark("LoadContent start");
        var spriteBatch = new SpriteBatch(GraphicsDevice);
        var pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData([Color.White]);
        StartupDiagnostics.Mark("SpriteBatch and pixel texture created");

        assets = FrontierAssetLoader.Load(GraphicsDevice);
        audioInitialization.Start();
        audioBus.Load(GraphicsDevice);
        titleMusicPlayer.Start();
        timeline.Initialize(world);
        updateCoordinator = new GameUpdateCoordinator(
            world,
            new KeyboardInputMapper(),
            timeline,
            new AudioFeedbackRouter(audioBus),
            new GameplayTransitionLogger());
        renderer = new GameRenderer(
            GraphicsDevice,
            spriteBatch,
            pixel,
            assets,
            timeline,
            new GameCamera());
        titleFilm = TitleFilmPlayer.TryLoad(
            GraphicsDevice,
            Path.Combine(AppContext.BaseDirectory, "Assets", "Video", SelectedTitleFilm()));
        titleRenderer = new TitleScreenRenderer(
            new RenderContext(GraphicsDevice, spriteBatch, pixel, assets, new GameCamera()),
            titleFilm);
        StartupDiagnostics.Mark("LoadContent complete");
    }

    protected override void Update(GameTime gameTime)
    {
        var measurement = telemetry.BeginUpdate(gameTime, world);
        if (telemetry.MarkFirstUpdate())
            StartupDiagnostics.Mark("first Update");

        titleMusicPlayer.Update();
        gameplayMusicPlayer.Update();
        if (!titleScreen.HasStarted)
        {
            var wasOnTitle = !titleScreen.HasStarted;
            titleScreen.Update(Keyboard.GetState().IsKeyDown(Keys.Enter));
            titleFilm?.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
            if (wasOnTitle && titleScreen.HasStarted)
            {
                titleMusicPlayer.Stop();
                gameplayMusicPlayer.Start();
            }
            base.Update(gameTime);
            telemetry.EndUpdate(measurement, gameTime, world);
            return;
        }
        updateCoordinator.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        base.Update(gameTime);
        telemetry.EndUpdate(measurement, gameTime, world);
    }

    protected override void Draw(GameTime gameTime)
    {
        var measurement = telemetry.BeginDraw(gameTime, world);
        if (telemetry.MarkFirstDraw())
            StartupDiagnostics.Mark("first Draw; startup ready");

        if (titleScreen.HasStarted)
            renderer.Draw(world);
        else
            titleRenderer.Draw();
        base.Draw(gameTime);
        telemetry.EndDraw(measurement, gameTime, world);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            titleFilm?.Dispose();
        base.Dispose(disposing);
    }

    internal static string SelectedTitleFilm() =>
        string.Equals(
            Environment.GetEnvironmentVariable("COWBANIA_TITLE_FILM"),
            "cinematic",
            StringComparison.OrdinalIgnoreCase)
            ? "cowbania_title_loop.cwvf"
            : "cowbania_title_pixel_loop.cwvf";
}
