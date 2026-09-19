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
    private readonly Dictionary<string, Texture2D> playerSprites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> banditSprites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> wildlifeSprites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> pickupSprites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> terrainSprites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> propSprites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> effectSprites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> uiSprites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> backgroundSprites = new(StringComparer.Ordinal);
    private readonly AnimationClock playerClock = new();
    private readonly Dictionary<string, PresentationAnimationClock> enemyClocks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BoundedEffectClock> defeatEffectClocks = new(StringComparer.Ordinal);
    private readonly Dictionary<PickupType, PresentationAnimationClock> pickupClocks = new();
    private readonly AudioEventBus audioBus = new();
    private RasterizerState scissorRasterizer = null!;
    private PresentationAnimationState playerAnimationState;
    private float shootTimer;
    private float hurtTimer;
    private float pickupEffectTimer;
    private System.Numerics.Vector2 pickupEffectPosition;
    private float presentationSeconds;
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
        scissorRasterizer = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
        StartupDiagnostics.Mark("SpriteBatch created");
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        StartupDiagnostics.Mark("pixel texture created");
        LoadActorSprites("Player", playerSprites, FrontierAnimationCatalog.PlayerClips.Values);
        StartupDiagnostics.Mark($"player sprites loaded ({playerSprites.Count})");
        LoadActorSprites("Bandit", banditSprites, FrontierAnimationCatalog.BanditClips.Values);
        LoadActorSprites("Wildlife", wildlifeSprites, FrontierAnimationCatalog.WildlifeClips.Values);
        StartupDiagnostics.Mark($"enemy sprites loaded (bandit={banditSprites.Count}, wildlife={wildlifeSprites.Count})");
        LoadActorSprites("Pickup", pickupSprites, FrontierAnimationCatalog.PickupClips.Values);
        StartupDiagnostics.Mark($"pickup sprites loaded ({pickupSprites.Count})");
        LoadNamedSprites("Terrain", terrainSprites, 16, 16,
            "ground_cap", "ground_body", "platform_left", "platform_middle", "platform_right",
            "timber_support", "stone", "mine_reinforcement");
        LoadNamedSprites("Props", propSprites, 16, 16, "cactus_0", "cactus_1", "crate", "sign");
        LoadNamedSprites("Props", propSprites, 32, 32,
            "checkpoint", "shortcut", "transition_gate", "wagon_debris", "mine_timber");
        LoadNamedSprites("Effects", effectSprites, 16, 16,
            "muzzle_0", "muzzle_1", "muzzle_2", "impact_0", "impact_1", "impact_2",
            "dust_0", "dust_1", "dust_2", "dash_0", "dash_1", "dash_2",
            "hurt_0", "hurt_1", "defeat_0", "defeat_1", "defeat_2",
            "pickup_0", "pickup_1", "pickup_2", "pickup_3");
        LoadNamedSprites("UI", uiSprites, 16, 16,
            "heart_full", "heart_empty", "ammo_full", "ammo_empty", "currency", "slot_frame", "panel_corner");
        LoadNamedSprites("Background", backgroundSprites, 256, 144,
            "hub_far", "hub_mid", "branch_far", "branch_mid");
        StartupDiagnostics.Mark(
            $"environment sprites loaded (terrain={terrainSprites.Count}, props={propSprites.Count}, " +
            $"effects={effectSprites.Count}, ui={uiSprites.Count}, backgrounds={backgroundSprites.Count})");
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
        var previousHealth = world.Health;
        var previousPickupCount = world.CollectedPickupCount;
        var previousReloading = world.IsReloading;
        var previousPaused = world.IsPaused;
        var previousRoom = world.Room;
        var previousObjective = world.ObjectivePhase;
        var previousCheckpointRoom = world.CheckpointRoom;
        var previousCheckpointPosition = world.CheckpointPosition;
        var previousEnemies = world.Enemies.ToArray();
        var previousProjectiles = world.Projectiles.ToArray();
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
        LogEncounterTransitions(previousEnemies, previousProjectiles);
        if (previousRoom != world.Room ||
            previousHealth == 1 && world.Health == GameWorld.MaximumHealth)
        {
            enemyClocks.Clear();
            defeatEffectClocks.Clear();
            pickupClocks.Clear();
            pickupEffectTimer = 0;
            shootTimer = 0;
            hurtTimer = 0;
            RuntimeLog.Info($"enemy presentation clocks reset frame={updateFrameCount} room={world.Room}");
        }
        var simulationActive = !world.IsPaused && !world.Completed;
        if (simulationActive)
        {
            var acceptedPlayerShot = world.PlayerShotAcceptedThisUpdate;
            if (world.LastJumpRequestOutcome == JumpRequestOutcome.Accepted)
            {
                RuntimeLog.Info($"jump audio dispatch frame={updateFrameCount} room={world.Room}");
                audioBus.Play(AudioEvent.Jump);
            }
            if (input.DashPressed && world.IsDashing) audioBus.Play(AudioEvent.Dash);
            if (!previousReloading && world.IsReloading) audioBus.Play(AudioEvent.Reload);
            if (acceptedPlayerShot) audioBus.Play(AudioEvent.Shooting);
            if (world.Health < previousHealth) audioBus.Play(AudioEvent.Damage);
            if (world.CollectedPickupCount > previousPickupCount) audioBus.Play(AudioEvent.Pickup);
            if (StartedEnemyAttack(previousEnemies, world.Enemies, EnemyArchetype.Bandit))
                audioBus.Play(AudioEvent.Shooting);
            if (StartedEnemyAttack(previousEnemies, world.Enemies, EnemyArchetype.Wildlife))
                audioBus.Play(AudioEvent.Dash);
            if (acceptedPlayerShot) shootTimer = 0.14f;
            if (world.Health < previousHealth) hurtTimer = 0.35f;
            if (world.CollectedPickupCount > previousPickupCount)
            {
                pickupEffectTimer = 0.32f;
                pickupEffectPosition = world.PlayerPosition;
            }
            shootTimer = MathF.Max(0, shootTimer - dt);
            hurtTimer = MathF.Max(0, hurtTimer - dt);
            pickupEffectTimer = MathF.Max(0, pickupEffectTimer - dt);
            presentationSeconds += dt;
            var state = PresentationStateSelector.SelectPlayer(new PlayerPresentationInput(
                MathF.Abs(world.PlayerVelocity.X) > 0.01f, world.IsGrounded, world.PlayerVelocity.Y < 0,
                shootTimer > 0, world.IsReloading, hurtTimer > 0, world.IsDashing));
            if (PlayerAnimationRestart.ShouldReset(playerAnimationState, state, acceptedPlayerShot))
            {
                playerAnimationState = state;
                playerClock.Reset();
            }
            playerClock.Advance(dt, FrontierAnimationCatalog.For(playerAnimationState));
            foreach (var enemy in world.Enemies)
            {
                if (!enemyClocks.TryGetValue(enemy.Id, out var clock))
                    enemyClocks[enemy.Id] = clock = new PresentationAnimationClock();
                clock.Advance(dt, enemy);
                if (enemy.Alive)
                {
                    defeatEffectClocks.Remove(enemy.Id);
                }
                else
                {
                    if (!defeatEffectClocks.TryGetValue(enemy.Id, out var defeatClock))
                        defeatEffectClocks[enemy.Id] = defeatClock = new BoundedEffectClock(3, 6f);
                    defeatClock.Advance(dt);
                }
            }
            foreach (var pickupType in FrontierAnimationCatalog.PickupClips.Keys)
            {
                if (!pickupClocks.TryGetValue(pickupType, out var clock))
                    pickupClocks[pickupType] = clock = new PresentationAnimationClock();
                clock.Advance(dt, pickupType);
            }
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
        DrawRoomSetDressing(room, palette, cameraX);
        foreach (var solid in room.Solids)
            DrawSolidSurface(solid, room, palette, cameraX);
        DrawLandmarks(room, palette, cameraX);
        var playerClip = FrontierAnimationCatalog.For(playerAnimationState);
        DrawActorSprite(playerSprites[playerClock.CurrentFrame(playerClip).AssetKey], world.PlayerPosition, cameraX, world.FacingDirection);
        DrawPlayerEffects(cameraX);
        for (var i = 0; i < world.Enemies.Count; i++)
        {
            var enemy = world.Enemies[i];
            var presentation = PresentationStateSelector.SelectEnemy(enemy);
            if (!enemyClocks.TryGetValue(enemy.Id, out var clock))
            {
                enemyClocks[enemy.Id] = clock = new PresentationAnimationClock();
                clock.Advance(0, enemy);
            }

            DrawEnemyTelegraph(enemy, presentation, cameraX);
            var enemyClip = FrontierAnimationCatalog.ForEnemy(enemy);
            var spriteCache = enemy.Archetype == EnemyArchetype.Bandit ? banditSprites : wildlifeSprites;
            DrawActorSprite(
                spriteCache[enemyClip.Frames[Math.Clamp(clock.CurrentFrameIndex, 0, enemyClip.Frames.Length - 1)].AssetKey],
                enemy.Position,
                cameraX,
                presentation.FacingDirection);
            DrawEnemyEffects(enemy, presentation, cameraX);
        }
        foreach (var projectile in world.Projectiles)
            DrawProjectile(projectile, cameraX);
        foreach (var pickup in world.AvailablePickups)
        {
            if (!pickupClocks.TryGetValue(pickup.Type, out var clock))
            {
                pickupClocks[pickup.Type] = clock = new PresentationAnimationClock();
                clock.Advance(0, pickup.Type);
            }
            var pickupTexture = pickupSprites[clock.CurrentFrame().AssetKey];
            DrawAnchoredSprite(
                pickupTexture,
                pickup.Position,
                cameraX,
                FrontierAnimationCatalog.PickupMetadata.SourceFeetAnchor,
                2f);
        }
        if (pickupEffectTimer > 0)
        {
            var pickupEffectFrame = Math.Min(3, (int)((0.32f - pickupEffectTimer) * 12.5f));
            DrawEffect("pickup", pickupEffectFrame, pickupEffectPosition, cameraX, 1, 2f);
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
            $"health={world.Health} ammo={world.Ammo} enemies={world.Enemies.Count(enemy => enemy.Alive)} " +
            $"projectiles={world.Projectiles.Count} hostileProjectiles={world.Projectiles.Count(projectile => projectile.Owner == ProjectileOwner.Enemy)}");
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

    private void LogEncounterTransitions(
        IReadOnlyList<EnemyState> previousEnemies,
        IReadOnlyList<ProjectileState> previousProjectiles)
    {
        var previousById = previousEnemies.ToDictionary(enemy => enemy.Id, StringComparer.Ordinal);
        foreach (var enemy in world.Enemies)
        {
            if (!previousById.TryGetValue(enemy.Id, out var previousEnemy))
            {
                RuntimeLog.Info(
                    $"enemy snapshot added id=\"{enemy.Id}\" archetype={enemy.Archetype} " +
                    $"state={enemy.BehaviorState} phase={enemy.AttackPhase} frame={updateFrameCount}");
                continue;
            }

            if (previousEnemy.BehaviorState != enemy.BehaviorState ||
                previousEnemy.AttackPhase != enemy.AttackPhase ||
                previousEnemy.Alive != enemy.Alive)
            {
                RuntimeLog.Info(
                    $"enemy transition id=\"{enemy.Id}\" archetype={enemy.Archetype} " +
                    $"state={previousEnemy.BehaviorState}->{enemy.BehaviorState} " +
                    $"phase={previousEnemy.AttackPhase}->{enemy.AttackPhase} " +
                    $"alive={previousEnemy.Alive}->{enemy.Alive} frame={updateFrameCount}");
            }
        }

        var previousHostileCount = previousProjectiles.Count(projectile => projectile.Owner == ProjectileOwner.Enemy);
        var hostileProjectiles = world.Projectiles
            .Where(projectile => projectile.Owner == ProjectileOwner.Enemy)
            .ToArray();
        if (hostileProjectiles.Length > previousHostileCount)
            RuntimeLog.Info(
                $"hostile projectiles count={previousHostileCount}->{hostileProjectiles.Length} " +
                $"sources=\"{string.Join(",", hostileProjectiles.Select(projectile => projectile.SourceId).Distinct())}\" " +
                $"frame={updateFrameCount}");
    }

    private static bool StartedEnemyAttack(
        IReadOnlyList<EnemyState> previousEnemies,
        IReadOnlyList<EnemyState> currentEnemies,
        EnemyArchetype archetype)
    {
        var previousById = previousEnemies.ToDictionary(enemy => enemy.Id, StringComparer.Ordinal);
        return currentEnemies.Any(enemy =>
            enemy.Archetype == archetype &&
            enemy.AttackPhase == EnemyAttackPhase.Active &&
            (!previousById.TryGetValue(enemy.Id, out var previousEnemy) ||
             previousEnemy.AttackPhase != EnemyAttackPhase.Active));
    }

    private static StagePalette GetPalette(int roomId) => roomId == RoomCatalog.Branch.Id ? BranchPalette : HubPalette;

    private void DrawRoomBackdrop(RoomDefinition room, StagePalette palette, float cameraX)
    {
        var roomRect = new Rectangle((int)(room.Bounds.X - cameraX), (int)room.Bounds.Y, (int)room.Bounds.Width, (int)room.Bounds.Height);
        DrawRect(roomRect, palette.SkyBottom);
        var prefix = room.Id == RoomCatalog.Branch.Id ? "branch" : "hub";
        DrawBackgroundBand(backgroundSprites[$"{prefix}_far"], cameraX, 0.08f);
        DrawBackgroundBand(backgroundSprites[$"{prefix}_mid"], cameraX, 0.18f);
    }

    private void DrawBackgroundBand(Texture2D texture, float cameraX, float parallax)
    {
        const int scale = 4;
        var tileWidth = texture.Width * scale;
        var startX = -(int)(cameraX * parallax) % tileWidth;
        if (startX > 0) startX -= tileWidth;
        for (var x = startX; x < GraphicsDevice.Viewport.Width; x += tileWidth)
            DrawSprite(texture, new Rectangle(x, 0, tileWidth, texture.Height * scale), Color.White);
    }

    private void DrawSolidSurface(RoomRect solid, RoomDefinition room, StagePalette palette, float cameraX)
    {
        var rect = ToScreen(solid, cameraX);
        var isGround = solid == room.Ground;
        DrawRect(rect, new Color(35, 24, 32));
        var viewport = new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        var clip = Rectangle.Intersect(rect, viewport);
        if (clip.Width <= 0 || clip.Height <= 0)
            return;

        spriteBatch.End();
        GraphicsDevice.ScissorRectangle = clip;
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: scissorRasterizer);
        var bodyTexture = terrainSprites[isGround ? "ground_body" : "stone"];
        foreach (var tile in TerrainTileLayout.Cover(rect))
            spriteBatch.Draw(bodyTexture, tile.Destination, tile.Source, Color.White);

        if (isGround)
            TileHorizontal(terrainSprites["ground_cap"], rect.X, rect.Y, rect.Width, 48);
        else
        {
            DrawTerrainTile(terrainSprites["platform_left"], rect.X, rect.Y);
            if (rect.Width > 96)
                TileHorizontal(terrainSprites["platform_middle"], rect.X + 48, rect.Y, rect.Width - 96, 48);
            DrawTerrainTile(terrainSprites["platform_right"], Math.Max(rect.X, rect.Right - 48), rect.Y);
        }

        spriteBatch.End();
        GraphicsDevice.ScissorRectangle = viewport;
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawRect(new Rectangle(rect.X, rect.Y, rect.Width, Math.Min(3, rect.Height)), new Color(239, 190, 95));
    }

    private void TileHorizontal(Texture2D texture, int x, int y, int width, int height)
    {
        for (var offset = 0; offset < width; offset += 48)
            DrawTerrainTile(texture, x + offset, y);
    }

    private void DrawTerrainTile(Texture2D texture, int x, int y) =>
        spriteBatch.Draw(texture, new Rectangle(x, y, 48, 48), new Rectangle(0, 0, 16, 16), Color.White);

    private void DrawLandmarks(RoomDefinition room, StagePalette palette, float cameraX)
    {
        DrawProp("transition_gate", new System.Numerics.Vector2(room.Bounds.X + 24, room.Ground.Y), cameraX, 3f);
        DrawProp("transition_gate", new System.Numerics.Vector2(room.Bounds.Right - 24, room.Ground.Y), cameraX, 3f, true);
        DrawProp("checkpoint", room.Checkpoint, cameraX, 3f);
        DrawProp("shortcut", room.Shortcut, cameraX, 3f);
    }

    private void DrawRoomSetDressing(RoomDefinition room, StagePalette palette, float cameraX)
    {
        var backgroundPropTint = new Color(145, 137, 140, 190);
        if (room.Id == RoomCatalog.Hub.Id)
        {
            DrawProp("sign", new System.Numerics.Vector2(room.Bounds.X + 360, room.Ground.Y), cameraX, 3f, tint: backgroundPropTint);
            DrawProp("cactus_0", new System.Numerics.Vector2(room.Bounds.X + 820, room.Ground.Y), cameraX, 3f, tint: backgroundPropTint);
            DrawProp("cactus_1", new System.Numerics.Vector2(room.Bounds.X + 1320, room.Ground.Y), cameraX, 3f, tint: backgroundPropTint);
            DrawProp("crate", new System.Numerics.Vector2(room.Bounds.X + 610, room.Ground.Y), cameraX, 2f, tint: backgroundPropTint);
        }
        else
        {
            DrawProp("mine_timber", new System.Numerics.Vector2(room.Bounds.X + 520, room.Ground.Y), cameraX, 3f, tint: backgroundPropTint);
            DrawProp("mine_timber", new System.Numerics.Vector2(room.Bounds.X + 1220, room.Ground.Y), cameraX, 3f, tint: backgroundPropTint);
            DrawProp("wagon_debris", new System.Numerics.Vector2(room.Bounds.X + 980, room.Ground.Y), cameraX, 3f, tint: backgroundPropTint);
            DrawProp("cactus_0", new System.Numerics.Vector2(room.Bounds.X + 280, room.Ground.Y), cameraX, 3f, tint: backgroundPropTint);
        }
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

    private void DrawEnemyTelegraph(
        EnemyState enemy,
        EnemyPresentationDefinition presentation,
        float cameraX)
    {
        if (presentation.TelegraphMarker == EnemyTelegraphMarker.None)
            return;

        var position = ToScreen(enemy.Position, cameraX);
        var x = (int)position.X;
        var feetY = (int)position.Y;
        var facing = presentation.FacingDirection;
        var progress = Math.Clamp(enemy.AttackTimerNormalized, 0f, 1f);

        switch (presentation.TelegraphMarker)
        {
            case EnemyTelegraphMarker.NoticeBurst:
                DrawRect(new Rectangle(x - 2, feetY - 66, 4, 14), Color.White);
                DrawRect(new Rectangle(x - 12, feetY - 62, 7, 4), Color.Gold);
                DrawRect(new Rectangle(x + 5, feetY - 62, 7, 4), Color.Gold);
                break;

            case EnemyTelegraphMarker.BanditAimLine:
                var aimLength = 40 + (int)(72 * progress);
                var aimStart = x + facing * 18;
                DrawRect(
                    new Rectangle(
                        facing > 0 ? aimStart : aimStart - aimLength,
                        feetY - 27,
                        aimLength,
                        2),
                    new Color(255, 196, 72, 190));
                DrawRect(new Rectangle(aimStart + facing * (aimLength - 3) - 2, feetY - 30, 5, 8), Color.Red);
                break;

            case EnemyTelegraphMarker.BanditMuzzleFlash:
                var muzzleX = x + facing * 24;
                DrawRect(new Rectangle(muzzleX - 6, feetY - 31, 12, 12), Color.Gold);
                DrawRect(new Rectangle(muzzleX - 2, feetY - 35, 4, 20), Color.White);
                break;

            case EnemyTelegraphMarker.WildlifeLungeArrow:
                var arrowX = x + facing * (28 + (int)(16 * progress));
                DrawRect(
                    new Rectangle(facing > 0 ? x + 12 : arrowX, feetY - 18, Math.Abs(arrowX - (x + facing * 12)), 4),
                    new Color(154, 255, 112, 210));
                DrawRect(new Rectangle(arrowX - 4, feetY - 24, 8, 16), Color.White);
                break;

            case EnemyTelegraphMarker.WildlifeLungeTrail:
                var trailX = facing > 0 ? x - 52 : x + 16;
                DrawRect(new Rectangle(trailX, feetY - 34, 36, 6), new Color(105, 220, 104, 150));
                DrawRect(new Rectangle(trailX - facing * 8, feetY - 22, 26, 4), new Color(180, 255, 150, 120));
                break;
        }
    }

    private void DrawHud()
    {
        DrawHudPanel(new Rectangle(10, 10, 248, 94));
        for (var i = 0; i < GameWorld.MaximumHealth; i++)
            DrawSprite(uiSprites[i < world.Health ? "heart_full" : "heart_empty"], new Rectangle(20 + i * 38, 18, 32, 32), Color.White);
        for (var i = 0; i < 6; i++)
            DrawSprite(uiSprites[i < world.Ammo ? "ammo_full" : "ammo_empty"], new Rectangle(20 + i * 26, 56, 24, 24), Color.White);
        if (world.IsReloading) DrawRect(new Rectangle(20, 84, 146, 4), new Color(239, 190, 95));

        DrawSprite(uiSprites["currency"], new Rectangle(184, 54, 32, 32), Color.White);
        DrawNumber(world.Currency, 220, 61, new Color(224, 204, 157));

        DrawHudPanel(new Rectangle(GraphicsDevice.Viewport.Width - 74, 10, 64, 64));
        DrawSprite(uiSprites["slot_frame"], new Rectangle(GraphicsDevice.Viewport.Width - 66, 18, 48, 48), Color.White);
        DrawDigit(1, GraphicsDevice.Viewport.Width - 50, 31, new Color(239, 190, 95), 4);

        if (world.IsPaused)
        {
            DrawRect(new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(20, 14, 20, 96));
            var panel = new Rectangle(GraphicsDevice.Viewport.Width / 2 - 96, 202, 192, 128);
            DrawHudPanel(panel);
            DrawRect(new Rectangle(panel.Center.X - 28, panel.Y + 32, 16, 64), new Color(224, 204, 157));
            DrawRect(new Rectangle(panel.Center.X + 12, panel.Y + 32, 16, 64), new Color(224, 204, 157));
        }

        if (world.Completed)
        {
            var panel = new Rectangle(GraphicsDevice.Viewport.Width / 2 - 176, 24, 352, 72);
            DrawHudPanel(panel);
            DrawRect(new Rectangle(panel.X + 32, panel.Y + 25, panel.Width - 64, 5), new Color(203, 133, 54));
            DrawRect(new Rectangle(panel.X + 54, panel.Y + 40, panel.Width - 108, 4), new Color(239, 190, 95));
        }
    }

    private void DrawHudPanel(Rectangle panel)
    {
        DrawRect(panel, new Color(35, 24, 32, 220));
        DrawRect(new Rectangle(panel.X + 4, panel.Y + 4, panel.Width - 8, panel.Height - 8), new Color(57, 35, 38, 230));
        var corner = uiSprites["panel_corner"];
        DrawSprite(corner, new Rectangle(panel.X, panel.Y, 32, 32), Color.White);
        DrawSprite(corner, new Rectangle(panel.Right - 32, panel.Y, 32, 32), Color.White, SpriteEffects.FlipHorizontally);
        DrawSprite(corner, new Rectangle(panel.X, panel.Bottom - 32, 32, 32), Color.White, SpriteEffects.FlipVertically);
        DrawSprite(corner, new Rectangle(panel.Right - 32, panel.Bottom - 32, 32, 32), Color.White, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically);
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
        int facingDirection,
        Color? tint = null)
    {
        var position = ToScreen(anchor, cameraX);
        var origin = new Vector2(
            FrontierAnimationCatalog.PlayerMetadata.SourceFeetAnchor.X,
            FrontierAnimationCatalog.PlayerMetadata.SourceFeetAnchor.Y);
        var effects = facingDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        spriteBatch.Draw(texture, position, null, tint ?? Color.White, 0f, origin, 3f, effects, 0f);
    }

    private void DrawSprite(Texture2D texture, Rectangle destination, Color color) =>
        spriteBatch.Draw(texture, destination, color);

    private void DrawSprite(Texture2D texture, Rectangle destination, Color color, SpriteEffects effects) =>
        spriteBatch.Draw(texture, destination, null, color, 0f, Vector2.Zero, effects, 0f);

    private void DrawAnchoredSprite(
        Texture2D texture,
        System.Numerics.Vector2 anchor,
        float cameraX,
        System.Numerics.Vector2 sourceAnchor,
        float scale,
        int facingDirection = 1,
        Color? tint = null)
    {
        var effects = facingDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        spriteBatch.Draw(texture, ToScreen(anchor, cameraX), null, tint ?? Color.White, 0f,
            new Vector2(sourceAnchor.X, sourceAnchor.Y), scale, effects, 0f);
    }

    private void DrawPlayerEffects(float cameraX)
    {
        if (world.IsDashing)
            DrawEffect("dash", FixedFrame(3, 15f), world.PlayerPosition, cameraX, -world.FacingDirection, 3f);
        else if (hurtTimer > 0)
        {
            var hurtEffectFrame = Math.Min(1, (int)((0.35f - hurtTimer) * 8f));
            DrawEffect("hurt", hurtEffectFrame, world.PlayerPosition, cameraX, world.FacingDirection, 3f);
        }
        else if (world.IsGrounded && MathF.Abs(world.PlayerVelocity.X) > 1f)
            DrawEffect("dust", FixedFrame(3, 10f), world.PlayerPosition, cameraX, -world.FacingDirection, 2f);

        if (shootTimer > 0)
        {
            var metadata = FrontierAnimationCatalog.PlayerMetadata;
            var effectPosition = world.PlayerPosition + new System.Numerics.Vector2(
                (metadata.SourceEffectAnchor.X - metadata.SourceFeetAnchor.X) * 3f * world.FacingDirection,
                (metadata.SourceEffectAnchor.Y - metadata.SourceFeetAnchor.Y) * 3f);
            var muzzleEffectFrame = Math.Min(2, (int)((0.14f - shootTimer) * 21f));
            DrawEffect("muzzle", muzzleEffectFrame, effectPosition, cameraX, world.FacingDirection, 2f);
        }
    }

    private void DrawEnemyEffects(EnemyState enemy, EnemyPresentationDefinition presentation, float cameraX)
    {
        if (!enemy.Alive)
        {
            if (!defeatEffectClocks.TryGetValue(enemy.Id, out var clock))
                defeatEffectClocks[enemy.Id] = clock = new BoundedEffectClock(3, 6f);
            if (clock.IsVisible)
                DrawEffect("defeat", clock.CurrentFrameIndex, enemy.Position, cameraX, presentation.FacingDirection, 3f);
            return;
        }

        if (!presentation.AttackActive)
            return;

        var metadata = enemy.Archetype == EnemyArchetype.Bandit
            ? FrontierAnimationCatalog.BanditMetadata
            : FrontierAnimationCatalog.WildlifeMetadata;
        var effectPosition = enemy.Position + new System.Numerics.Vector2(
            (metadata.SourceEffectAnchor.X - metadata.SourceFeetAnchor.X) * 3f * presentation.FacingDirection,
            (metadata.SourceEffectAnchor.Y - metadata.SourceFeetAnchor.Y) * 3f);
        var attackEffectFrame = Math.Min(2, (int)(Math.Clamp(enemy.AttackTimerNormalized, 0f, 0.999f) * 3f));
        DrawEffect(
            enemy.Archetype == EnemyArchetype.Bandit ? "muzzle" : "dash",
            attackEffectFrame,
            effectPosition,
            cameraX,
            presentation.FacingDirection,
            2f);
    }

    private void DrawProjectile(ProjectileState projectile, float cameraX)
    {
        var visual = ProjectileVisualCatalog.For(projectile.Owner);
        var direction = projectile.Velocity.LengthSquared() > 0
            ? System.Numerics.Vector2.Normalize(projectile.Velocity)
            : System.Numerics.Vector2.UnitX;
        var angle = MathF.Atan2(direction.Y, direction.X);
        var position = ToScreen(projectile.Position, cameraX);
        spriteBatch.Draw(
            pixel,
            position,
            null,
            visual.PrimaryColor,
            angle,
            new Vector2(0.5f, 0.5f),
            new Vector2(visual.Length, visual.Thickness),
            SpriteEffects.None,
            0f);

        if (visual.Shape == ProjectileVisualShape.PlayerTracer)
        {
            spriteBatch.Draw(
                pixel,
                position,
                null,
                visual.SecondaryColor,
                angle,
                new Vector2(0.5f, 0.5f),
                new Vector2(visual.Length - 4, 1),
                SpriteEffects.None,
                0f);
            return;
        }

        var tip = position + new Vector2(direction.X, direction.Y) * (visual.Length / 2f);
        spriteBatch.Draw(
            pixel,
            tip,
            null,
            visual.SecondaryColor,
            angle + MathF.PI / 4f,
            new Vector2(0.5f, 0.5f),
            new Vector2(5, 5),
            SpriteEffects.None,
            0f);
    }

    private int FixedFrame(int frameCount, float framesPerSecond) =>
        (int)(presentationSeconds * framesPerSecond) % frameCount;

    private void DrawEffect(
        string effect,
        int frame,
        System.Numerics.Vector2 anchor,
        float cameraX,
        int facingDirection,
        float scale)
    {
        var texture = effectSprites[$"{effect}_{frame}"];
        DrawAnchoredSprite(texture, anchor, cameraX, new System.Numerics.Vector2(8, 8), scale, facingDirection);
    }

    private void DrawProp(
        string name,
        System.Numerics.Vector2 anchor,
        float cameraX,
        float scale,
        bool flip = false,
        Color? tint = null) =>
        DrawAnchoredSprite(
            propSprites[name],
            anchor,
            cameraX,
            new System.Numerics.Vector2(propSprites[name].Width / 2f, propSprites[name].Height),
            scale,
            flip ? -1 : 1,
            tint);

    private void LoadActorSprites(
        string category,
        Dictionary<string, Texture2D> cache,
        IEnumerable<AnimationClip> clips)
    {
        foreach (var assetKey in clips.SelectMany(clip => clip.Frames).Select(frame => frame.AssetKey).Distinct(StringComparer.Ordinal))
            cache[assetKey] = LoadFrontierSprite(assetKey, 16, 16, category);
    }

    private void LoadNamedSprites(
        string category,
        Dictionary<string, Texture2D> cache,
        int expectedWidth,
        int expectedHeight,
        params string[] names)
    {
        foreach (var name in names)
            cache[name] = LoadFrontierSprite($"Frontier/{category}/{name}.png", expectedWidth, expectedHeight, category);
    }

    private Texture2D LoadFrontierSprite(string assetKey, int expectedWidth, int expectedHeight, string category)
    {
        var normalizedKey = assetKey.Replace('/', Path.DirectorySeparatorChar);
        var relativePath = Path.Combine("Assets", "Art", normalizedKey);
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, relativePath),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath))
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var texture = Texture2D.FromFile(GraphicsDevice, path);
                if (texture.Width != expectedWidth || texture.Height != expectedHeight)
                {
                    var actualWidth = texture.Width;
                    var actualHeight = texture.Height;
                    texture.Dispose();
                    throw new InvalidDataException(
                        $"Frontier {category} asset '{relativePath}' has invalid dimensions. " +
                        $"Expected {expectedWidth}x{expectedHeight}, found {actualWidth}x{actualHeight}.");
                }
                return texture;
            }
            catch (Exception exception) when (exception is not InvalidDataException)
            {
                throw new InvalidDataException(
                    $"Frontier {category} asset '{relativePath}' could not be decoded as a valid PNG.",
                    exception);
            }
        }

        throw new FileNotFoundException(
            $"Required Frontier {category} asset '{relativePath}' was not found. " +
            $"Checked packaged path '{candidates[0]}' and repository path '{candidates[1]}'.",
            candidates[0]);
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

internal readonly record struct TerrainTileDraw(Rectangle Destination, Rectangle Source);

internal static class TerrainTileLayout
{
    public const int SourceTileSize = 16;
    public const int PixelScale = 3;
    public const int DestinationTileSize = SourceTileSize * PixelScale;

    public static IEnumerable<TerrainTileDraw> Cover(Rectangle clip)
    {
        for (var y = clip.Y; y < clip.Bottom; y += DestinationTileSize)
        for (var x = clip.X; x < clip.Right; x += DestinationTileSize)
            yield return new TerrainTileDraw(
                new Rectangle(x, y, DestinationTileSize, DestinationTileSize),
                new Rectangle(0, 0, SourceTileSize, SourceTileSize));
    }
}

internal static class PlayerAnimationRestart
{
    public static bool ShouldReset(
        PresentationAnimationState previousState,
        PresentationAnimationState selectedState,
        bool acceptedPlayerShot) =>
        selectedState != previousState ||
        acceptedPlayerShot && selectedState == PresentationAnimationState.Shoot;
}

internal sealed class BoundedEffectClock(int frameCount, float framesPerSecond)
{
    private float elapsedSeconds;

    public int CurrentFrameIndex =>
        Math.Min(frameCount - 1, (int)(elapsedSeconds * framesPerSecond));

    public bool IsVisible => elapsedSeconds < frameCount / framesPerSecond;

    public void Advance(float elapsedSeconds)
    {
        if (elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        this.elapsedSeconds = Math.Min(frameCount / framesPerSecond, this.elapsedSeconds + elapsedSeconds);
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
    private static readonly ProjectileVisualDefinition Player = new(
        ProjectileVisualShape.PlayerTracer,
        new Color(255, 211, 92),
        Color.White,
        16,
        3);

    private static readonly ProjectileVisualDefinition Hostile = new(
        ProjectileVisualShape.HostileBolt,
        new Color(190, 48, 64),
        new Color(255, 174, 92),
        12,
        6);

    public static ProjectileVisualDefinition For(ProjectileOwner owner) =>
        owner == ProjectileOwner.Player ? Player : Hostile;
}
