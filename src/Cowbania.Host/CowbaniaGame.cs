using System.Numerics;
using System.IO;
using System.Diagnostics;
using Cowbania.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = Microsoft.Xna.Framework.Vector2;

internal sealed class CowbaniaGame : Game
{
    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch spriteBatch = null!;
    private Texture2D pixel = null!;
    private readonly Dictionary<string, Texture2D> playerSprites = new();
    private readonly Dictionary<string, Texture2D> enemySprites = new();
    private readonly Dictionary<string, Texture2D> pickupSprites = new();
    private readonly AnimationClock playerClock = new();
    private readonly Dictionary<int, AnimationClock> enemyClocks = new();
    private readonly AnimationClock pickupClock = new();
    private readonly AudioEventBus audioBus = new();
    private PresentationAnimationState playerAnimationState;
    private float shootTimer;
    private float hurtTimer;
    private GameWorld world = new();
    private KeyboardState previous;
    private bool loggedFirstUpdate;
    private bool loggedFirstDraw;
    private readonly Stopwatch runtimeClock = Stopwatch.StartNew();
    private long updateFrameCount;
    private long drawFrameCount;
    private double lastUpdateHeartbeatSeconds = -2;
    private double lastDrawHeartbeatSeconds = -2;
    private double lastUpdateDurationMilliseconds;
    private double lastDrawDurationMilliseconds;

    private static readonly StagePalette HubPalette = new(
        new Color(24, 30, 46),
        new Color(73, 90, 115),
        new Color(133, 156, 177),
        new Color(51, 66, 63),
        new Color(89, 110, 104),
        new Color(140, 160, 145),
        new Color(32, 109, 82),
        new Color(68, 140, 98),
        new Color(19, 62, 47),
        new Color(108, 80, 62),
        new Color(149, 115, 82),
        new Color(69, 51, 39),
        new Color(240, 202, 95),
        new Color(135, 78, 42),
        new Color(180, 155, 121));

    private static readonly StagePalette BranchPalette = new(
        new Color(18, 24, 38),
        new Color(52, 54, 79),
        new Color(104, 120, 154),
        new Color(46, 40, 62),
        new Color(78, 77, 104),
        new Color(121, 117, 146),
        new Color(44, 72, 77),
        new Color(62, 104, 103),
        new Color(22, 49, 52),
        new Color(90, 68, 88),
        new Color(130, 100, 120),
        new Color(66, 49, 62),
        new Color(213, 168, 94),
        new Color(114, 82, 59),
        new Color(181, 162, 170));

    public CowbaniaGame()
    {
        StartupDiagnostics.Mark("CowbaniaGame constructor start");
        graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 1024, PreferredBackBufferHeight = 576 };
        Window.Title = "Cowbania";
        graphics.IsFullScreen = false;
        graphics.HardwareModeSwitch = false;
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
        spriteBatch = new SpriteBatch(GraphicsDevice);
        StartupDiagnostics.Mark("SpriteBatch created");
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        StartupDiagnostics.Mark("pixel texture created");
        LoadSprites("Player", playerSprites, new[] { "idle_0.png", "idle_1.png", "run_0.png", "run_1.png", "jump_0.png", "fall_0.png", "shoot_0.png", "shoot_1.png", "reload_0.png", "reload_1.png", "hurt_0.png" });
        StartupDiagnostics.Mark($"player sprites loaded ({playerSprites.Count})");
        LoadSprites("Enemy", enemySprites, new[] { "idle_0.png", "idle_1.png" });
        StartupDiagnostics.Mark($"enemy sprites loaded ({enemySprites.Count})");
        LoadSprites("Pickup", pickupSprites, new[] { "float_0.png", "float_1.png" });
        StartupDiagnostics.Mark($"pickup sprites loaded ({pickupSprites.Count})");
        audioBus.Load(GraphicsDevice);
        playerAnimationState = PresentationAnimationState.Idle;
        StartupDiagnostics.Mark("LoadContent complete");
    }

    protected override void Update(GameTime gameTime)
    {
        var updateStarted = Stopwatch.GetTimestamp();
        updateFrameCount++;
        LogUpdateHeartbeat(gameTime);
        if (!loggedFirstUpdate)
        {
            loggedFirstUpdate = true;
            StartupDiagnostics.Mark("first Update");
        }
        var keyboard = Keyboard.GetState();
        var aim = System.Numerics.Vector2.Zero;
        if (keyboard.IsKeyDown(Keys.Up)) aim.Y--;
        if (keyboard.IsKeyDown(Keys.Down)) aim.Y++;
        if (keyboard.IsKeyDown(Keys.Left)) aim.X--;
        if (keyboard.IsKeyDown(Keys.Right)) aim.X++;
        var input = new InputFrame(
            (keyboard.IsKeyDown(Keys.D) ? 1 : 0) - (keyboard.IsKeyDown(Keys.A) ? 1 : 0),
            Pressed(keyboard, Keys.Space), Pressed(keyboard, Keys.LeftShift),
            aim, keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl),
            Pressed(keyboard, Keys.R), Pressed(keyboard, Keys.E), Pressed(keyboard, Keys.Escape),
            Pressed(keyboard, Keys.D1));
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var previousAmmo = world.Ammo;
        var previousHealth = world.Health;
        var previousPickupCount = world.CollectedPickupCount;
        var previousReloading = world.IsReloading;
        var previousPaused = world.IsPaused;
        var previousRoom = world.Room;
        var previousObjective = world.ObjectivePhase;
        var previousCheckpointRoom = world.CheckpointRoom;
        var previousCheckpointPosition = world.CheckpointPosition;
        if (input.JumpPressed)
            RuntimeLog.Info(
                $"jump request frame={updateFrameCount} room={world.Room} " +
                $"position=({world.PlayerPosition.X:F1},{world.PlayerPosition.Y:F1}) " +
                $"velocity=({world.PlayerVelocity.X:F1},{world.PlayerVelocity.Y:F1}) " +
                $"grounded={world.IsGrounded} dashing={world.IsDashing} paused={world.IsPaused} completed={world.Completed}");
        world.Update(input, dt);
        if (input.JumpPressed)
            RuntimeLog.Info(
                $"jump {world.LastJumpRequestOutcome} frame={updateFrameCount} room={world.Room} " +
                $"position=({world.PlayerPosition.X:F1},{world.PlayerPosition.Y:F1}) " +
                $"velocity=({world.PlayerVelocity.X:F1},{world.PlayerVelocity.Y:F1}) " +
                $"grounded={world.IsGrounded} dashing={world.IsDashing} paused={world.IsPaused} completed={world.Completed}");
        LogStateTransitions(
            previousPaused,
            previousRoom,
            previousObjective,
            previousHealth,
            previousCheckpointRoom,
            previousCheckpointPosition);
        var simulationActive = !world.IsPaused && !world.Completed;
        if (simulationActive)
        {
            if (world.LastJumpRequestOutcome == JumpRequestOutcome.Accepted)
            {
                RuntimeLog.Info($"jump audio dispatch frame={updateFrameCount} room={world.Room}");
                audioBus.Play(AudioEvent.Jump);
            }
            if (input.DashPressed && world.IsDashing) audioBus.Play(AudioEvent.Dash);
            if (!previousReloading && world.IsReloading) audioBus.Play(AudioEvent.Reload);
            if (world.Ammo < previousAmmo) audioBus.Play(AudioEvent.Shooting);
            if (world.Health < previousHealth) audioBus.Play(AudioEvent.Damage);
            if (world.CollectedPickupCount > previousPickupCount) audioBus.Play(AudioEvent.Pickup);
            if (world.Ammo < previousAmmo) shootTimer = 0.14f;
            if (world.Health < previousHealth) hurtTimer = 0.35f;
            shootTimer = MathF.Max(0, shootTimer - dt);
            hurtTimer = MathF.Max(0, hurtTimer - dt);
            var state = PresentationStateSelector.SelectPlayer(new PlayerPresentationInput(
                input.Horizontal != 0, world.IsGrounded, world.PlayerVelocity.Y < 0,
                shootTimer > 0, world.IsReloading, hurtTimer > 0, world.IsDashing));
            if (state != playerAnimationState)
            {
                playerAnimationState = state;
                playerClock.Reset();
            }
            playerClock.Advance(dt, PlaceholderAnimationCatalog.For(playerAnimationState));
            for (var i = 0; i < world.Enemies.Count; i++)
            {
                var enemy = world.Enemies[i];
                var index = i;
                if (!enemyClocks.TryGetValue(index, out var clock))
                    enemyClocks[index] = clock = new AnimationClock();
                clock.Advance(dt, PlaceholderAnimationCatalog.For(PresentationStateSelector.SelectEnemy(enemy.Alive)));
            }
            pickupClock.Advance(dt, PlaceholderAnimationCatalog.For(PresentationAnimationState.PickupFloat));
        }
        previous = keyboard;
        base.Update(gameTime);
        lastUpdateDurationMilliseconds = Stopwatch.GetElapsedTime(updateStarted).TotalMilliseconds;
        if (dt >= 0.1f || lastUpdateDurationMilliseconds >= 50)
            RuntimeLog.Warn(
                $"slow Update frame={updateFrameCount} gameElapsedMs={dt * 1000:F1} " +
                $"executionMs={lastUpdateDurationMilliseconds:F1} room={world.Room} paused={world.IsPaused}");
    }

    private bool Pressed(KeyboardState state, Keys key) => state.IsKeyDown(key) && !previous.IsKeyDown(key);

    protected override void Draw(GameTime gameTime)
    {
        var drawStarted = Stopwatch.GetTimestamp();
        drawFrameCount++;
        LogDrawHeartbeat(gameTime);
        if (!loggedFirstDraw)
        {
            loggedFirstDraw = true;
            StartupDiagnostics.Mark("first Draw; startup ready");
        }
        var room = world.CurrentRoom;
        var viewportWidth = GraphicsDevice.Viewport.Width;
        var cameraMax = Math.Max(0, room.Bounds.Right - viewportWidth);
        var cameraX = Math.Clamp(world.PlayerPosition.X - viewportWidth / 2f, room.Bounds.X, cameraMax);
        var palette = GetPalette(room.Id);
        GraphicsDevice.Clear(palette.SkyTop);
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawRoomBackdrop(room, palette, cameraX);
        foreach (var solid in room.Solids)
            DrawSolidSurface(solid, room, palette, cameraX);
        DrawLandmarks(room, palette, cameraX);
        DrawRoomSetDressing(room, palette, cameraX);
        var playerClip = PlaceholderAnimationCatalog.For(playerAnimationState);
        DrawActorSprite(playerSprites[playerClock.CurrentFrame(playerClip).AssetKey], world.PlayerPosition, cameraX, world.FacingDirection);
        for (var i = 0; i < world.Enemies.Count; i++)
        {
            var enemy = world.Enemies[i];
            if (!enemy.Alive) continue;
            var clip = PlaceholderAnimationCatalog.For(PresentationStateSelector.SelectEnemy(true));
            DrawActorSprite(enemySprites[clip.Frames[enemyClocks[i].CurrentFrameIndex].AssetKey], enemy.Position, cameraX, 1);
        }
        foreach (var projectile in world.Projectiles)
        {
            var projectileScreenPosition = ToScreen(projectile.Position, cameraX);
            DrawRect(new Rectangle((int)projectileScreenPosition.X - 3, (int)projectileScreenPosition.Y - 3, 6, 6), Color.Yellow);
        }
        var pickupClip = PlaceholderAnimationCatalog.For(PresentationAnimationState.PickupFloat);
        var pickupTexture = pickupSprites[pickupClip.Frames[pickupClock.CurrentFrameIndex].AssetKey];
        foreach (var pickup in world.AvailablePickups)
        {
            var tint = pickup.Type switch
            {
                PickupType.Currency => Color.Gold,
                PickupType.Health => Color.LightGreen,
                PickupType.ReserveAmmo => Color.Orange,
                _ => Color.White
            };
            DrawSprite(pickupTexture, new Rectangle(
                (int)(pickup.Position.X - cameraX - 16),
                (int)pickup.Position.Y - 32,
                32,
                32), tint);
        }
        DrawTerrainForeground(room, palette, cameraX);
        DrawHud();
        spriteBatch.End();
        base.Draw(gameTime);
        lastDrawDurationMilliseconds = Stopwatch.GetElapsedTime(drawStarted).TotalMilliseconds;
        if (lastDrawDurationMilliseconds >= 50)
            RuntimeLog.Warn(
                $"slow Draw frame={drawFrameCount} gameElapsedMs={gameTime.ElapsedGameTime.TotalMilliseconds:F1} " +
                $"executionMs={lastDrawDurationMilliseconds:F1} room={world.Room}");
    }

    private void LogUpdateHeartbeat(GameTime gameTime)
    {
        var runtimeSeconds = runtimeClock.Elapsed.TotalSeconds;
        if (runtimeSeconds - lastUpdateHeartbeatSeconds < 2)
            return;

        lastUpdateHeartbeatSeconds = runtimeSeconds;
        RuntimeLog.Info(
            $"Update heartbeat begin frame={updateFrameCount} runtimeSeconds={runtimeSeconds:F1} " +
            $"gameElapsedMs={gameTime.ElapsedGameTime.TotalMilliseconds:F1} previousExecutionMs={lastUpdateDurationMilliseconds:F1} " +
            $"room={world.Room} objective={world.ObjectivePhase} paused={world.IsPaused} completed={world.Completed} " +
            $"health={world.Health} ammo={world.Ammo} projectiles={world.Projectiles.Count}");
    }

    private void LogDrawHeartbeat(GameTime gameTime)
    {
        var runtimeSeconds = runtimeClock.Elapsed.TotalSeconds;
        if (runtimeSeconds - lastDrawHeartbeatSeconds < 2)
            return;

        lastDrawHeartbeatSeconds = runtimeSeconds;
        RuntimeLog.Info(
            $"Draw heartbeat begin frame={drawFrameCount} runtimeSeconds={runtimeSeconds:F1} " +
            $"gameElapsedMs={gameTime.ElapsedGameTime.TotalMilliseconds:F1} previousExecutionMs={lastDrawDurationMilliseconds:F1} " +
            $"updateFrame={updateFrameCount} room={world.Room}");
    }

    private void LogStateTransitions(
        bool previousPaused,
        int previousRoom,
        ObjectivePhase previousObjective,
        int previousHealth,
        int previousCheckpointRoom,
        System.Numerics.Vector2 previousCheckpointPosition)
    {
        if (previousPaused != world.IsPaused)
            RuntimeLog.Info($"state pause {previousPaused}->{world.IsPaused} frame={updateFrameCount}");

        if (previousRoom != world.Room)
            RuntimeLog.Info(
                $"state room {previousRoom}->{world.Room} frame={updateFrameCount} " +
                $"position=({world.PlayerPosition.X:F1},{world.PlayerPosition.Y:F1})");

        if (previousObjective != world.ObjectivePhase)
            RuntimeLog.Info($"state objective {previousObjective}->{world.ObjectivePhase} frame={updateFrameCount}");

        if (previousCheckpointRoom != world.CheckpointRoom ||
            previousCheckpointPosition != world.CheckpointPosition)
            RuntimeLog.Info(
                $"state checkpoint room={previousCheckpointRoom}->{world.CheckpointRoom} " +
                $"position=({previousCheckpointPosition.X:F1},{previousCheckpointPosition.Y:F1})->" +
                $"({world.CheckpointPosition.X:F1},{world.CheckpointPosition.Y:F1})");

        if (previousHealth == 1 && world.Health == GameWorld.MaximumHealth)
            RuntimeLog.Warn(
                $"state death/respawn frame={updateFrameCount} room={world.Room} " +
                $"position=({world.PlayerPosition.X:F1},{world.PlayerPosition.Y:F1})");
        else if (previousHealth != world.Health)
            RuntimeLog.Info($"state health {previousHealth}->{world.Health} frame={updateFrameCount}");
    }

    private static StagePalette GetPalette(int roomId) => roomId == RoomCatalog.Branch.Id ? BranchPalette : HubPalette;

    private void DrawRoomBackdrop(RoomDefinition room, StagePalette palette, float cameraX)
    {
        var roomRect = new Rectangle((int)(room.Bounds.X - cameraX), (int)room.Bounds.Y, (int)room.Bounds.Width, (int)room.Bounds.Height);
        DrawRect(roomRect, palette.SkyBottom);

        var sunX = roomRect.X + (room.Id == RoomCatalog.Branch.Id ? 760 : 180);
        var sunY = room.Id == RoomCatalog.Branch.Id ? 92 : 58;
        DrawRect(new Rectangle(sunX, sunY, 52, 52), palette.Accent);
        DrawRect(new Rectangle(sunX + 12, sunY + 12, 28, 28), palette.AccentDark);

        var ridgeOffset = (int)(cameraX * 0.12f) % 160;
        for (var i = 0; i < 6; i++)
        {
            var ridgeX = roomRect.X - 80 + i * 220 - ridgeOffset;
            var ridgeWidth = 180 + (i % 3) * 28;
            DrawRect(new Rectangle(ridgeX, 260 + (i % 2) * 18, ridgeWidth, 170), palette.RidgeBack);
            DrawRect(new Rectangle(ridgeX + 20, 276 + (i % 2) * 18, ridgeWidth - 40, 150), palette.RidgeMid);
        }

        DrawRect(new Rectangle(roomRect.X - 30, 330, roomRect.Width + 60, 120), palette.RidgeFront);
        DrawRect(new Rectangle(roomRect.X - 30, 380, roomRect.Width + 60, 100), palette.RidgeBack);

        if (room.Id == RoomCatalog.Branch.Id)
        {
            var caveX = roomRect.X + 1040 - (int)(cameraX * 0.18f);
            DrawRect(new Rectangle(caveX, 190, 210, 170), palette.LandmarkShadow);
            DrawRect(new Rectangle(caveX + 18, 214, 174, 146), palette.RidgeBack);
            DrawRect(new Rectangle(caveX + 38, 246, 134, 114), palette.SkyTop);
        }

        for (var i = 0; i < 9; i++)
        {
            var cloudX = roomRect.X + 110 + i * 180 - (int)(cameraX * 0.06f) % 120;
            DrawRect(new Rectangle(cloudX, 90 + (i % 3) * 18, 54, 12), palette.SkyMid);
            DrawRect(new Rectangle(cloudX + 18, 84 + (i % 3) * 18, 22, 10), palette.SkyMid);
        }
    }

    private void DrawSolidSurface(RoomRect solid, RoomDefinition room, StagePalette palette, float cameraX)
    {
        var rect = ToScreen(solid, cameraX);
        var isGround = solid == room.Ground;
        DrawRect(rect, isGround ? palette.GroundBody : palette.PlatformBody);
        DrawRect(new Rectangle(rect.X, rect.Y, rect.Width, Math.Min(10, rect.Height)), isGround ? palette.GroundTop : palette.PlatformTop);
        DrawRect(new Rectangle(rect.Right - 5, rect.Y, 5, rect.Height), isGround ? palette.GroundEdge : palette.PlatformEdge);

        var tileWidth = isGround ? 24 : 20;
        var tileHeight = isGround ? 18 : 12;
        for (var x = rect.X; x < rect.Right; x += tileWidth)
        {
            var tileOffset = ((x / tileWidth) & 1) == 0 ? 0 : tileHeight / 2;
            for (var y = rect.Y + (isGround ? 14 : 10) + tileOffset; y < rect.Bottom; y += tileHeight)
            {
                var width = Math.Min(tileWidth - 3, rect.Right - x - 2);
                var height = Math.Min(tileHeight - 3, rect.Bottom - y - 2);
                if (width > 0 && height > 0)
                    DrawRect(new Rectangle(x + 2, y, width, height), isGround ? palette.GroundEdge : palette.PlatformEdge);
            }
        }
    }

    private void DrawLandmarks(RoomDefinition room, StagePalette palette, float cameraX)
    {
        DrawTransitionDoor(room.Bounds.X + 18, room, palette, cameraX);
        DrawTransitionDoor(room.Bounds.Right - 18, room, palette, cameraX);
        DrawCheckpointMarker(room.Checkpoint, palette, cameraX, room.Id == 1);
        DrawShortcutMarker(room.Shortcut, palette, cameraX);
    }

    private void DrawRoomSetDressing(RoomDefinition room, StagePalette palette, float cameraX)
    {
        if (room.Id == RoomCatalog.Hub.Id)
        {
            DrawWaterTower(room.Bounds.X + 360, 228, palette, cameraX);
            DrawCactus(room.Bounds.X + 820, 414, palette, cameraX);
            DrawCactus(room.Bounds.X + 1320, 418, palette, cameraX);
        }
        else
        {
            DrawMineTimbers(room.Bounds.X + 520, 318, palette, cameraX);
            DrawMineTimbers(room.Bounds.X + 1220, 332, palette, cameraX);
            DrawCactus(room.Bounds.X + 280, 418, palette, cameraX);
        }
    }

    private void DrawWaterTower(float anchorX, int y, StagePalette palette, float cameraX)
    {
        var x = (int)(anchorX - cameraX);
        DrawRect(new Rectangle(x - 28, y, 56, 12), palette.LandmarkShadow);
        DrawRect(new Rectangle(x - 22, y + 10, 44, 42), palette.PlatformBody);
        DrawRect(new Rectangle(x - 18, y + 16, 36, 4), palette.PlatformEdge);
        DrawRect(new Rectangle(x - 16, y + 52, 8, 56), palette.PlatformEdge);
        DrawRect(new Rectangle(x + 8, y + 52, 8, 56), palette.PlatformEdge);
        DrawRect(new Rectangle(x - 32, y - 8, 64, 8), palette.AccentDark);
    }

    private void DrawMineTimbers(float anchorX, int y, StagePalette palette, float cameraX)
    {
        var x = (int)(anchorX - cameraX);
        DrawRect(new Rectangle(x - 42, y, 8, 132), palette.PlatformEdge);
        DrawRect(new Rectangle(x + 34, y, 8, 132), palette.PlatformEdge);
        DrawRect(new Rectangle(x - 50, y + 8, 100, 8), palette.PlatformBody);
        DrawRect(new Rectangle(x - 34, y + 28, 8, 74), palette.PlatformBody);
        DrawRect(new Rectangle(x + 26, y + 28, 8, 74), palette.PlatformBody);
    }

    private void DrawCactus(float anchorX, int y, StagePalette palette, float cameraX)
    {
        var x = (int)(anchorX - cameraX);
        DrawRect(new Rectangle(x - 5, y - 44, 10, 44), palette.GroundEdge);
        DrawRect(new Rectangle(x - 18, y - 30, 10, 8), palette.GroundEdge);
        DrawRect(new Rectangle(x - 18, y - 38, 8, 16), palette.GroundEdge);
        DrawRect(new Rectangle(x + 8, y - 20, 10, 8), palette.GroundEdge);
        DrawRect(new Rectangle(x + 10, y - 30, 8, 18), palette.GroundEdge);
    }

    private void DrawTerrainForeground(RoomDefinition room, StagePalette palette, float cameraX)
    {
        var ground = ToScreen(room.Ground, cameraX);
        for (var x = ground.X + 6; x < ground.Right; x += 48)
        {
            var blade = ((x / 48) & 1) == 0 ? 6 : 10;
            DrawRect(new Rectangle(x, ground.Y - blade, 3, blade), palette.GroundTop);
            DrawRect(new Rectangle(x + 6, ground.Y - Math.Max(4, blade - 3), 3, Math.Max(4, blade - 3)), palette.GroundTop);
        }
    }

    private void DrawTransitionDoor(float anchorX, RoomDefinition room, StagePalette palette, float cameraX)
    {
        var x = (int)(anchorX - cameraX);
        var y = (int)room.Bounds.Y + 160;
        DrawRect(new Rectangle(x - 18, y, 36, 266), palette.LandmarkShadow);
        DrawRect(new Rectangle(x - 12, y + 14, 24, 220), palette.PlatformBody);
        DrawRect(new Rectangle(x - 18, y + 16, 36, 8), palette.AccentDark);
        DrawRect(new Rectangle(x - 8, y + 70, 16, 104), palette.Accent);
    }

    private void DrawCheckpointMarker(System.Numerics.Vector2 checkpoint, StagePalette palette, float cameraX, bool active)
    {
        var x = (int)(checkpoint.X - cameraX);
        var y = (int)checkpoint.Y;
        DrawRect(new Rectangle(x - 4, y - 58, 8, 54), palette.LandmarkShadow);
        DrawRect(new Rectangle(x - 18, y - 70, 36, 12), active ? palette.Accent : palette.AccentDark);
        DrawRect(new Rectangle(x - 10, y - 52, 20, 10), active ? palette.AccentDark : palette.SkyMid);
        DrawRect(new Rectangle(x - 2, y - 84, 4, 18), palette.Accent);
    }

    private void DrawShortcutMarker(System.Numerics.Vector2 shortcut, StagePalette palette, float cameraX)
    {
        var x = (int)(shortcut.X - cameraX);
        var y = (int)shortcut.Y;
        DrawRect(new Rectangle(x - 12, y - 90, 24, 90), palette.LandmarkShadow);
        DrawRect(new Rectangle(x - 26, y - 102, 52, 14), palette.AccentDark);
        DrawRect(new Rectangle(x - 20, y - 92, 40, 10), palette.Accent);
        DrawRect(new Rectangle(x - 2, y - 118, 4, 20), palette.Accent);
    }

    private void DrawHud()
    {
        for (var i = 0; i < world.Health; i++) DrawRect(new Rectangle(16 + i * 18, 16, 14, 14), Color.Red);
        for (var i = 0; i < world.Ammo; i++) DrawRect(new Rectangle(16 + i * 12, 38, 8, 8), Color.Gold);
        if (world.IsReloading) DrawRect(new Rectangle(16, 54, 80, 6), Color.White);

        DrawRect(new Rectangle(16, 70, 14, 14), Color.Gold);
        DrawRect(new Rectangle(20, 74, 6, 6), new Color(120, 82, 24));
        DrawNumber(world.Currency, 36, 70, Color.White);

        var slotColor = world.SelectedWeaponSlot == 1 ? Color.Gold : Color.Gray;
        DrawRect(new Rectangle(962, 14, 42, 42), slotColor);
        DrawRect(new Rectangle(966, 18, 34, 34), new Color(24, 30, 46));
        DrawDigit(1, 978, 23, slotColor, 4);

        if (world.IsPaused)
        {
            DrawRect(new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 140));
            DrawRect(new Rectangle(474, 226, 24, 124), Color.White);
            DrawRect(new Rectangle(526, 226, 24, 124), Color.White);
        }

        if (world.Completed)
        {
            DrawRect(new Rectangle(352, 30, 320, 54), new Color(30, 24, 12, 230));
            DrawRect(new Rectangle(360, 38, 304, 38), Color.Gold);
            DrawRect(new Rectangle(368, 46, 288, 22), new Color(30, 24, 12));
            DrawRect(new Rectangle(474, 50, 12, 12), Color.Gold);
            DrawRect(new Rectangle(486, 58, 12, 12), Color.Gold);
            DrawRect(new Rectangle(498, 46, 42, 12), Color.Gold);
        }
    }

    private void DrawNumber(int value, int x, int y, Color color)
    {
        var digits = Math.Max(0, value).ToString();
        foreach (var digit in digits)
        {
            DrawDigit(digit - '0', x, y, color, 3);
            x += 12;
        }
    }

    private void DrawDigit(int digit, int x, int y, Color color, int scale)
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
                DrawRect(new Rectangle(x + column * scale, y + row * scale, scale, scale), color);
    }
    private void DrawActorSprite(
        Texture2D texture,
        System.Numerics.Vector2 anchor,
        float cameraX,
        int facingDirection)
    {
        var position = ToScreen(anchor, cameraX);
        var origin = new Vector2(texture.Width / 2f, 13f);
        var effects = facingDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        spriteBatch.Draw(texture, position, null, Color.White, 0f, origin, 3f, effects, 0f);
    }

    private void DrawSprite(Texture2D texture, Rectangle destination, Color color) =>
        spriteBatch.Draw(texture, destination, color);

    private void LoadSprites(string category, Dictionary<string, Texture2D> cache, IEnumerable<string> fileNames)
    {
        foreach (var fileName in fileNames)
            cache[fileName] = LoadSprite(category, fileName);
    }

    private Texture2D LoadSprite(string category, string fileName)
    {
        var relativePath = Path.Combine("Assets", "Art", "Placeholders", category, fileName);
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, relativePath),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath))
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return Texture2D.FromFile(GraphicsDevice, path);
        }

        throw new FileNotFoundException($"Sprite asset '{relativePath}' was not found.", candidates[0]);
    }

    private void DrawRect(Rectangle rectangle, Color color) => spriteBatch.Draw(pixel, rectangle, color);

    private static Rectangle ToScreen(RoomRect rect, float cameraX) =>
        new((int)(rect.X - cameraX), (int)rect.Y, (int)rect.Width, (int)rect.Height);

    private static Vector2 ToScreen(System.Numerics.Vector2 position, float cameraX) =>
        new(position.X - cameraX, position.Y);

    private readonly record struct StagePalette(
        Color SkyTop,
        Color SkyMid,
        Color SkyBottom,
        Color RidgeBack,
        Color RidgeMid,
        Color RidgeFront,
        Color GroundTop,
        Color GroundBody,
        Color GroundEdge,
        Color PlatformTop,
        Color PlatformBody,
        Color PlatformEdge,
        Color Accent,
        Color AccentDark,
        Color LandmarkShadow);
}
