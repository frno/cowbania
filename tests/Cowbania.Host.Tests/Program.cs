using System.Buffers.Binary;
using System.IO.Compression;
using Cowbania.Core;

static class Tests
{
    static readonly List<string> Failures = [];

    static void Main()
    {
        DeleteDiagnosticLogs();
        Assert(RuntimeLog.Initialize(), $"runtime logger initializes: {RuntimeLog.InitializationError}");

        Run("fatal reporting deduplicates only the same exception reference", () =>
        {
            var repeated = new InvalidOperationException("same-reference-failure");
            var distinctFirst = new InvalidOperationException("distinct-failure-one");
            var distinctSecond = new InvalidOperationException("distinct-failure-two");

            RuntimeLog.ReportProcessException("host-test-repeat", repeated, isTerminating: true);
            RuntimeLog.ReportProcessException("host-test-repeat", repeated, isTerminating: true);
            RuntimeLog.ReportProcessException("host-test-distinct-one", distinctFirst, isTerminating: true);
            RuntimeLog.ReportProcessException("host-test-distinct-two", distinctSecond, isTerminating: true);

            var log = ReadRuntimeLog();
            Assert(Count(log, "source=\"host-test-repeat\"") == 1, "the same exception instance logs once");
            Assert(log.Contains("distinct-failure-one") && log.Contains("distinct-failure-two"),
                "distinct exception instances are never suppressed by an identity hash collision");

            var startupLog = File.ReadAllText(StartupLogPath);
            Assert(Count(startupLog, "same-reference-failure") == 1,
                "the startup fallback records the same exception instance once");
            Assert(startupLog.Contains("distinct-failure-one") && startupLog.Contains("distinct-failure-two"),
                "the startup fallback retains distinct exception instances");
        });

        Run("aggregate and unobserved task failures include every detail and are observed", () =>
        {
            var aggregate = new AggregateException(
                "unobserved aggregate",
                new InvalidOperationException("decode inner"),
                new ArgumentException("playback inner"));
            var args = new UnobservedTaskExceptionEventArgs(aggregate);

            FatalExceptionHandlers.ReportUnobservedTaskException(args);

            var log = ReadRuntimeLog();
            Assert(args.Observed, "unobserved task exceptions are explicitly marked observed after logging");
            Assert(log.Contains("source=\"TaskScheduler.UnobservedTaskException\""),
                "unobserved task source is recorded");
            Assert(log.Contains("terminating=False"), "unobserved task termination policy is recorded");
            Assert(log.Contains("decode inner") && log.Contains("playback inner") &&
                   log.Contains("aggregateInner[0].ToString()") && log.Contains("aggregateInner[1].ToString()"),
                "all aggregate details are preserved");
        });

        Run("fatal reports flush runtime and startup sinks", () =>
        {
            RuntimeLog.ReportProcessException(
                "host-test-flush",
                new InvalidOperationException("flush-durability-marker"),
                isTerminating: true);

            Assert(ReadRuntimeLog().Contains("flush-durability-marker"), "runtime sink is durable after report returns");
            Assert(File.ReadAllText(StartupLogPath).Contains("flush-durability-marker"),
                "startup fallback sink is durable after report returns");
        });

        Run("audio playback failure has a terminal failure boundary", () =>
        {
            new AudioEventBus().Play(AudioEvent.Jump);
            var log = ReadRuntimeLog();
            Assert(log.Contains("audio playback request event=Jump"), "playback request is logged");
            Assert(log.Contains("audio playback failure event=Jump stage=decode-or-unavailable"),
                "unavailable playback emits a failure boundary");
        });

        Run("audio playback exception logs an event-only failure boundary", () =>
        {
            var audio = new AudioEventBus(new Dictionary<AudioEvent, IAudioPlayback>
            {
                [AudioEvent.Jump] = new ThrowingPlayback()
            });

            audio.Play(AudioEvent.Jump);

            var log = ReadRuntimeLog();
            Assert(log.Contains("audio playback begin event=Jump"), "playback begin is logged");
            Assert(log.Contains("audio playback failure event=Jump"), "playback failure is logged");
            Assert(log.Contains("test playback failure"), "playback failure retains exception details");
            Assert(log.Contains("audio playback failure event=Jump stage=playback disabled=true fallback=silence"),
                "throwing playback disables the event and selects silence");

            audio.Play(AudioEvent.Jump);
            log = ReadRuntimeLog();
            Assert(Count(log, "test playback failure") == 1,
                "a disabled throwing event is not invoked again");
            Assert(log.Contains("audio playback suppressed event=Jump reason=disabled fallback=silence"),
                "later calls use the silent fallback");
        });

        Run("managed WAV decoding accepts PCM without native file decoding", () =>
        {
            var wav = ManagedPcmWav.Read(CreatePcmWav([0, 1, 2, 3], sampleRate: 22050, channels: 1));

            Assert(wav.SampleRate == 22050, "managed decoding preserves the sample rate");
            Assert(wav.Channels == Microsoft.Xna.Framework.Audio.AudioChannels.Mono,
                "managed decoding preserves the channel layout");
            Assert(wav.PcmData.SequenceEqual(new byte[] { 0, 1, 2, 3 }),
                "managed decoding extracts the PCM payload");
        });

        Run("bundled jump audio is valid managed PCM", () =>
        {
            var jumpPath = Path.Combine(FindRepositoryRoot(), "Assets", "Audio", "SFX_Jump.wav");
            var wav = ManagedPcmWav.Read(File.ReadAllBytes(jumpPath));

            Assert(wav.SampleRate == 44100, "jump audio preserves its authored sample rate");
            Assert(wav.Channels == Microsoft.Xna.Framework.Audio.AudioChannels.Mono,
                "jump audio preserves its authored mono layout");
            Assert(wav.PcmData.Length > 0, "jump audio contains PCM samples");
        });

        Run("managed WAV decoding rejects invalid input", () =>
        {
            var exception = AssertThrows<InvalidDataException>(
                () => ManagedPcmWav.Read("not a wave"u8),
                "invalid WAV input fails through the managed decoder");
            Assert(exception.Message.Contains("RIFF/WAVE"), "managed failure explains the invalid container");
        });

        Run("successful audio load is cached and played", () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "managed-audio-success.wav");
            File.WriteAllBytes(path, CreatePcmWav([0, 0], sampleRate: 44100, channels: 1));
            try
            {
                var playback = new RecordingPlayback();
                var loader = new RecordingLoader(playback);
                var audio = new AudioEventBus(
                    null,
                    loader,
                    _ => path,
                    initialized: true);

                audio.Play(AudioEvent.Jump);
                audio.Play(AudioEvent.Jump);

                Assert(loader.LoadCount == 1, "a successfully loaded event is cached");
                Assert(playback.PlayCount == 2, "the cached sound plays on later requests");
                var log = ReadRuntimeLog();
                Assert(log.Contains("deferred managed audio decode complete event=Jump"),
                    "managed decode success is logged");
                Assert(log.Contains("audio playback result event=Jump played=True"),
                    "successful playback has a terminal result");
            }
            finally
            {
                File.Delete(path);
            }
        });

        Run("unsuccessful playback disables retries and falls back to silence", () =>
        {
            var playback = new RecordingPlayback(result: false);
            var audio = new AudioEventBus(new Dictionary<AudioEvent, IAudioPlayback>
            {
                [AudioEvent.Jump] = playback
            });

            audio.Play(AudioEvent.Jump);
            audio.Play(AudioEvent.Jump);

            Assert(playback.PlayCount == 1, "an unsuccessful playback result is not retried");
            var log = ReadRuntimeLog();
            Assert(log.Contains("audio playback result event=Jump played=False"),
                "an unsuccessful result has a terminal result boundary");
            Assert(log.Contains("audio playback failure event=Jump stage=playback-result disabled=true fallback=silence"),
                "an unsuccessful result disables the event and selects silence");
        });

        Run("decode failure disables retries and falls back to silence", () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "managed-audio-failure.wav");
            File.WriteAllBytes(path, "invalid"u8.ToArray());
            try
            {
                var loader = new ThrowingLoader();
                var audio = new AudioEventBus(
                    null,
                    loader,
                    _ => path,
                    initialized: true);

                audio.Play(AudioEvent.Jump);
                audio.Play(AudioEvent.Jump);

                Assert(loader.LoadCount == 1, "a failed decode is never retried");
                var log = ReadRuntimeLog();
                Assert(log.Contains("audio decode failure event=Jump"), "decode exception details are logged");
                Assert(log.Contains("audio playback failure event=Jump stage=decode-or-unavailable disabled=true fallback=silence"),
                    "decode failure records the terminal silent fallback");
                Assert(log.Contains("audio playback suppressed event=Jump reason=disabled fallback=silence"),
                    "later requests remain responsive and silent");
            }
            finally
            {
                File.Delete(path);
            }
        });

        Run("Frontier runtime has no placeholder asset references", () =>
        {
            var root = FindRepositoryRoot();
            var hostSource = ReadSource(root, "src", "Cowbania.Host", "CowbaniaGame.cs");
            var hostProject = ReadSource(root, "src", "Cowbania.Host", "Cowbania.Host.csproj");

            Assert(!hostSource.Contains("Placeholders", StringComparison.OrdinalIgnoreCase),
                "the Host runtime must not reference the retired Placeholders asset tree");
            Assert(!hostProject.Contains("Placeholders", StringComparison.OrdinalIgnoreCase),
                "the Host project must not copy retired placeholder art");
            Assert(AllAnimationAssetKeys().All(key => key.StartsWith("Frontier/", StringComparison.Ordinal)),
                "every runtime animation key resolves inside the Frontier pack");
        });

        Run("Frontier manifest and PNG inventory are complete", () =>
        {
            var root = FindRepositoryRoot();
            var frontierRoot = Path.Combine(root, "Assets", "Art", "Frontier");
            var expected = ExpectedFrontierAssets();
            var actual = Directory.GetFiles(frontierRoot, "*.png", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Path.Combine(root, "Assets", "Art"), path).Replace('\\', '/'))
                .ToHashSet(StringComparer.Ordinal);

            Assert(expected.SetEquals(actual),
                $"Frontier PNG inventory differs. Missing=[{string.Join(", ", expected.Except(actual).Order())}] " +
                $"Unexpected=[{string.Join(", ", actual.Except(expected).Order())}]");
            Assert(File.Exists(Path.Combine(frontierRoot, "manifest.md")),
                "the authored Frontier manifest must ship with the source pack");
        });

        Run("Frontier PNGs have valid RGBA signatures dimensions and transparency", () =>
        {
            var artRoot = Path.Combine(FindRepositoryRoot(), "Assets", "Art");
            foreach (var asset in ExpectedFrontierAssets())
            {
                var path = Path.Combine(artRoot, asset.Replace('/', Path.DirectorySeparatorChar));
                var png = ReadPng(path);
                var expectedSize = ExpectedPngSize(asset);

                Assert((png.Width, png.Height) == expectedSize,
                    $"{asset} must be {expectedSize.Width}x{expectedSize.Height}, found {png.Width}x{png.Height}");
                Assert(png.BitDepth == 8 && png.ColorType == 6 && png.InterlaceMethod == 0,
                    $"{asset} must be a non-interlaced 8-bit RGBA PNG");
                Assert(png.MaximumAlpha == 255, $"{asset} must contain visible opaque artwork");
                var opaqueFillTile =
                    asset.EndsWith("/Terrain/ground_body.png", StringComparison.Ordinal) ||
                    asset.EndsWith("/Terrain/stone.png", StringComparison.Ordinal);
                Assert(opaqueFillTile || png.MinimumAlpha == 0,
                    $"{asset} must contain transparent background pixels");
            }
        });

        Run("Frontier actor and pickup mappings are distinct", () =>
        {
            var bandit = FrontierAnimationCatalog.BanditClips.Values
                .SelectMany(clip => clip.Frames).Select(frame => frame.AssetKey).ToHashSet(StringComparer.Ordinal);
            var wildlife = FrontierAnimationCatalog.WildlifeClips.Values
                .SelectMany(clip => clip.Frames).Select(frame => frame.AssetKey).ToHashSet(StringComparer.Ordinal);
            Assert(!bandit.Overlaps(wildlife), "bandit and wildlife must not share sprite mappings");
            Assert(bandit.All(key => key.StartsWith("Frontier/Bandit/", StringComparison.Ordinal)),
                "bandit clips must resolve to Bandit art");
            Assert(wildlife.All(key => key.StartsWith("Frontier/Wildlife/", StringComparison.Ordinal)),
                "wildlife clips must resolve to Wildlife art");

            var pickupSets = Enum.GetValues<PickupType>().ToDictionary(
                type => type,
                type => FrontierAnimationCatalog.ForPickup(type).Frames
                    .Select(frame => frame.AssetKey).ToHashSet(StringComparer.Ordinal));
            foreach (var left in pickupSets)
            foreach (var right in pickupSets)
                if (left.Key < right.Key)
                    Assert(!left.Value.Overlaps(right.Value),
                        $"{left.Key} and {right.Key} pickups must use distinct icon frames");
        });

        Run("presentation clocks are deterministic and gated by pause or completion", () =>
        {
            var first = new PresentationAnimationClock();
            var second = new PresentationAnimationClock();
            var enemy = new EnemyState(
                "bandit-test", EnemyArchetype.Bandit, EnemyBehaviorState.Attack, EnemyAttackPhase.Telegraph,
                new System.Numerics.Vector2(100, 480), System.Numerics.Vector2.Zero, 1, 2, true, 0.5f, 0.25f);
            foreach (var dt in new[] { 0.01f, 0.07f, 0.13f, 0.02f })
            {
                first.Advance(dt, enemy);
                second.Advance(dt, enemy);
            }
            Assert(first.CurrentFrame() == second.CurrentFrame() &&
                   first.CurrentFrameIndex == second.CurrentFrameIndex,
                "equivalent snapshots and elapsed-time sequences must select the same presentation frame");

            var beforeGate = first.CurrentFrameIndex;
            AdvancePresentation(first, enemy, 1f, paused: true, completed: false);
            AdvancePresentation(first, enemy, 1f, paused: false, completed: true);
            Assert(first.CurrentFrameIndex == beforeGate,
                "paused and completed presentation gates must not advance clocks");

            var root = FindRepositoryRoot();
            var updateBody = MethodBody(ReadSource(root, "src", "Cowbania.Host", "CowbaniaGame.cs"), "Update");
            var gateIndex = updateBody.IndexOf("if (simulationActive)", StringComparison.Ordinal);
            var advances = System.Text.RegularExpressions.Regex.Matches(updateBody, @"\w+\.Advance\(dt")
                .Select(match => match.Index).ToArray();
            Assert(updateBody.Contains("!world.IsPaused && !world.Completed", StringComparison.Ordinal) &&
                   gateIndex >= 0 && advances.Length >= 3 && advances.All(index => index > gateIndex),
                "Host presentation clocks must advance only inside the shared pause/completion gate");
        });

        Run("terrain tiles keep integer scale and clip partial solid edges", () =>
        {
            var solid = new Microsoft.Xna.Framework.Rectangle(250, 380, 220, 24);
            var tiles = TerrainTileLayout.Cover(solid).ToArray();

            Assert(tiles.Length > 0, "raised terrain must produce tile draws");
            Assert(tiles.All(tile =>
                    tile.Destination.Width == tile.Source.Width * TerrainTileLayout.PixelScale &&
                    tile.Destination.Height == tile.Source.Height * TerrainTileLayout.PixelScale),
                "every terrain source pixel must render at the same integer scale");
            Assert(tiles.All(tile =>
                    tile.Destination.Width == TerrainTileLayout.DestinationTileSize &&
                    tile.Destination.Height == TerrainTileLayout.DestinationTileSize),
                "partial surface dimensions must not rescale individual tiles");
            Assert(tiles.Max(tile => tile.Destination.Right) >= solid.Right &&
                   tiles.Max(tile => tile.Destination.Bottom) >= solid.Bottom,
                "full-size edge tiles must cover the exact solid for destination clipping");

            var source = ReadSource(FindRepositoryRoot(), "src", "Cowbania.Host", "CowbaniaGame.cs");
            var drawSolid = MethodBody(source, "DrawSolidSurface");
            Assert(drawSolid.Contains("Rectangle.Intersect(rect, viewport)", StringComparison.Ordinal) &&
                   drawSolid.Contains("GraphicsDevice.ScissorRectangle = clip", StringComparison.Ordinal) &&
                   drawSolid.Contains("TerrainTileLayout.Cover(rect)", StringComparison.Ordinal),
                "terrain drawing must clip full integer-scaled tiles to the RoomCatalog solid rectangle");
        });

        Run("projectiles have persistent visuals distinct from transient effects", () =>
        {
            var player = ProjectileVisualCatalog.For(ProjectileOwner.Player);
            var hostile = ProjectileVisualCatalog.For(ProjectileOwner.Enemy);

            Assert(player.Shape == ProjectileVisualShape.PlayerTracer &&
                   hostile.Shape == ProjectileVisualShape.HostileBolt,
                "player and hostile projectiles need distinct directional silhouettes");
            Assert(player.PrimaryColor != hostile.PrimaryColor &&
                   player.SecondaryColor != hostile.SecondaryColor &&
                   (player.Length, player.Thickness) != (hostile.Length, hostile.Thickness),
                "projectile ownership must remain readable by color and geometry");

            var source = ReadSource(FindRepositoryRoot(), "src", "Cowbania.Host", "CowbaniaGame.cs");
            var draw = MethodBody(source, "Draw");
            var projectile = MethodBody(source, "DrawProjectile");
            Assert(draw.Contains("DrawProjectile(projectile, cameraX)", StringComparison.Ordinal),
                "persistent projectile snapshots must use the dedicated projectile renderer");
            Assert(!projectile.Contains("DrawEffect", StringComparison.Ordinal) &&
                   !projectile.Contains("\"muzzle\"", StringComparison.Ordinal) &&
                   !projectile.Contains("\"impact\"", StringComparison.Ordinal),
                "muzzle and impact sprites must remain transient effects, never projectile bodies");
        });

        Run("every accepted repeated shot restarts the one-shot animation", () =>
        {
            Assert(PlayerAnimationRestart.ShouldReset(
                    PresentationAnimationState.Shoot,
                    PresentationAnimationState.Shoot,
                    acceptedPlayerShot: true),
                "a second accepted shot must restart Shoot even when the selected state is unchanged");
            Assert(!PlayerAnimationRestart.ShouldReset(
                    PresentationAnimationState.Shoot,
                    PresentationAnimationState.Shoot,
                    acceptedPlayerShot: false),
                "held fire without an ammo decrement must not restart the animation");
            Assert(!PlayerAnimationRestart.ShouldReset(
                    PresentationAnimationState.Hurt,
                    PresentationAnimationState.Hurt,
                    acceptedPlayerShot: true),
                "an accepted shot must not override a higher-precedence selected state");

            var update = MethodBody(
                ReadSource(FindRepositoryRoot(), "src", "Cowbania.Host", "CowbaniaGame.cs"),
                "Update");
            Assert(update.Contains("var acceptedPlayerShot = world.PlayerShotAcceptedThisUpdate", StringComparison.Ordinal) &&
                   update.Contains("if (acceptedPlayerShot) audioBus.Play(AudioEvent.Shooting)", StringComparison.Ordinal) &&
                   update.Contains("PlayerAnimationRestart.ShouldReset", StringComparison.Ordinal),
                "shooting audio and animation restart must consume the deterministic core signal");
        });

        Run("reload-completion shot signal drives host feedback despite ammo increase", () =>
        {
            var game = new GameWorld();
            for (var shot = 0; shot < 6; shot++)
            {
                game.Update(new InputFrame(
                        0, false, false, System.Numerics.Vector2.UnitX, true, false, false, false),
                    shot == 0 ? 0f : GameWorld.FireDelay);
            }

            game.Update(new InputFrame(
                    0, false, false, System.Numerics.Vector2.UnitX, true, false, false, false),
                GameWorld.ReloadDuration - GameWorld.FireDelay);

            Assert(game.Ammo == 5 && game.PlayerShotAcceptedThisUpdate,
                "core exposes the accepted shot when reload completion raises ammo from empty to five");
            Assert(PlayerAnimationRestart.ShouldReset(
                    PresentationAnimationState.Shoot,
                    PresentationAnimationState.Shoot,
                    game.PlayerShotAcceptedThisUpdate),
                "the host restarts Shoot from the explicit signal even when Shoot is already selected");
        });

        Run("reload gameplay and presentation complete on the same update", () =>
        {
            const float startingUpdateSeconds = 0.08f;
            var game = new GameWorld();
            var reloadAnimation = FrontierAnimationCatalog.For(PresentationAnimationState.Reload);
            var animationClock = new AnimationClock();

            game.Update(new InputFrame(0, false, false, System.Numerics.Vector2.UnitX, true, false, false, false), 0f);
            game.Update(new InputFrame(0, false, false, System.Numerics.Vector2.UnitX, false, true, false, false),
                startingUpdateSeconds);
            animationClock.Advance(startingUpdateSeconds, reloadAnimation);

            Assert(game.IsReloading && !animationClock.IsComplete,
                "a reload begun on a nonzero update remains active in gameplay and presentation");

            var completingUpdateSeconds = GameWorld.ReloadDuration - startingUpdateSeconds + 0.0001f;
            game.Update(default, completingUpdateSeconds);
            animationClock.Advance(completingUpdateSeconds, reloadAnimation);

            Assert(!game.IsReloading && game.Ammo == 6 && animationClock.IsComplete,
                "gameplay refill and the Frontier reload one-shot complete on the same update");
        });

        Run("defeat effects play three frames once and stop", () =>
        {
            var clock = new BoundedEffectClock(3, 6f);
            Assert(clock.IsVisible && clock.CurrentFrameIndex == 0, "defeat starts on frame zero");
            clock.Advance(0.17f);
            Assert(clock.IsVisible && clock.CurrentFrameIndex == 1, "defeat advances to frame one");
            clock.Advance(0.17f);
            Assert(clock.IsVisible && clock.CurrentFrameIndex == 2, "defeat advances to frame two");
            clock.Advance(0.17f);
            Assert(!clock.IsVisible && clock.CurrentFrameIndex == 2,
                "defeat stops after its final frame instead of wrapping");
            clock.Advance(10f);
            Assert(!clock.IsVisible && clock.CurrentFrameIndex == 2,
                "completed defeat effects remain bounded");

            var source = ReadSource(FindRepositoryRoot(), "src", "Cowbania.Host", "CowbaniaGame.cs");
            var update = MethodBody(source, "Update");
            var effects = MethodBody(source, "DrawEnemyEffects");
            Assert(source.Contains(
                       "Dictionary<string, BoundedEffectClock> defeatEffectClocks",
                       StringComparison.Ordinal) &&
                   update.Contains("defeatEffectClocks.Clear()", StringComparison.Ordinal),
                "defeat clocks must be keyed by stable enemy ID and reset with the encounter");
            Assert(effects.Contains("clock.IsVisible", StringComparison.Ordinal) &&
                   !effects.Contains("FixedFrame", StringComparison.Ordinal),
                "defeat drawing must stop after the bounded clock completes");
        });

        Run("Frontier renderer preserves sampling geometry depth and snapshot contracts", () =>
        {
            var root = FindRepositoryRoot();
            var source = ReadSource(root, "src", "Cowbania.Host", "CowbaniaGame.cs");
            var draw = MethodBody(source, "Draw");
            var solid = MethodBody(source, "DrawSolidSurface");

            Assert(source.Contains("SamplerState.PointClamp", StringComparison.Ordinal),
                "Frontier rendering must use PointClamp");
            var fractionalScaleCalls = System.Text.RegularExpressions.Regex.Matches(
                    source, @"(?:DrawActorSprite|DrawAnchoredSprite|DrawEffect|DrawProp)\s*\([^;]*,\s*\d+\.\d+f\s*\)",
                    System.Text.RegularExpressions.RegexOptions.Singleline)
                .Select(match => System.Text.RegularExpressions.Regex.Replace(match.Value, @"\s+", " "))
                .ToArray();
            Assert(fractionalScaleCalls.Length == 0,
                $"Frontier sprites and effects must use integer scale factors. Found: {string.Join(" | ", fractionalScaleCalls)}");
            Assert(source.Contains("PresentationStateSelector.SelectEnemy(enemy)", StringComparison.Ordinal) &&
                   source.Contains("FrontierAnimationCatalog.ForEnemy(enemy)", StringComparison.Ordinal),
                "enemy presentation must be selected from deterministic enemy snapshots");
            Assert(!source.Contains("new Random", StringComparison.Ordinal) &&
                   !source.Contains("DateTime.", StringComparison.Ordinal),
                "presentation must not introduce nondeterministic random or wall-clock selection");
            Assert(draw.Contains("foreach (var solid in room.Solids)", StringComparison.Ordinal) &&
                   solid.Contains("ToScreen(solid, cameraX)", StringComparison.Ordinal),
                "rendered terrain must consume RoomCatalog solids directly");

            AssertInOrder(draw,
                "DrawRoomBackdrop", "DrawRoomSetDressing", "DrawSolidSurface", "DrawLandmarks",
                "DrawActorSprite", "world.Projectiles", "world.AvailablePickups",
                "DrawTerrainForeground", "DrawHud");
        });

        Run("Frontier output copy and explicit missing asset behavior are enforced", () =>
        {
            var root = FindRepositoryRoot();
            var project = ReadSource(root, "src", "Cowbania.Host", "Cowbania.Host.csproj");
            Assert(project.Contains(@"Assets\Art\Frontier\**\*.png", StringComparison.Ordinal) &&
                   project.Contains("CopyToOutputDirectory=\"PreserveNewest\"", StringComparison.Ordinal) &&
                   project.Contains("%(RecursiveDir)", StringComparison.Ordinal),
                "the Host project must recursively preserve the Frontier asset tree in build output");

            var outputArt = Path.Combine(root, "src", "Cowbania.Host", "bin", "Debug", "net10.0", "Assets", "Art");
            foreach (var asset in ExpectedFrontierAssets())
                Assert(File.Exists(Path.Combine(outputArt, asset.Replace('/', Path.DirectorySeparatorChar))),
                    $"required output asset is missing: {asset}");

            var loader = MethodBody(ReadSource(root, "src", "Cowbania.Host", "CowbaniaGame.cs"), "LoadFrontierSprite");
            Assert(loader.Contains("throw new FileNotFoundException", StringComparison.Ordinal) &&
                   loader.Contains("Required Frontier", StringComparison.Ordinal) &&
                   loader.Contains("relativePath", StringComparison.Ordinal) &&
                   !loader.Contains("Placeholders", StringComparison.OrdinalIgnoreCase),
                "missing Frontier art must fail with the exact relative asset path and no placeholder fallback");
        });

        Run("HUD and combat telegraphs include non-color identity cues", () =>
        {
            var source = ReadSource(FindRepositoryRoot(), "src", "Cowbania.Host", "CowbaniaGame.cs");
            var hud = MethodBody(source, "DrawHud");
            foreach (var icon in new[] { "heart_full", "heart_empty", "ammo_full", "ammo_empty", "currency", "slot_frame" })
                Assert(hud.Contains($"\"{icon}\"", StringComparison.Ordinal), $"HUD must use the {icon} Frontier icon");

            var bandit = PresentationStateSelector.SelectEnemy(new EnemyState(
                "bandit", EnemyArchetype.Bandit, EnemyBehaviorState.Attack, EnemyAttackPhase.Telegraph,
                default, default, 1, 2, true, 0, 0.5f));
            var wildlife = PresentationStateSelector.SelectEnemy(new EnemyState(
                "wildlife", EnemyArchetype.Wildlife, EnemyBehaviorState.Attack, EnemyAttackPhase.Telegraph,
                default, default, 1, 2, true, 0, 0.5f));
            Assert(bandit.TelegraphMarker == EnemyTelegraphMarker.BanditAimLine &&
                   wildlife.TelegraphMarker == EnemyTelegraphMarker.WildlifeLungeArrow &&
                   bandit.AnimationState != wildlife.AnimationState,
                "bandit and wildlife telegraphs must differ by geometry and pose, not tint alone");
            var telegraph = MethodBody(source, "DrawEnemyTelegraph");
            Assert(telegraph.Contains("BanditAimLine", StringComparison.Ordinal) &&
                   telegraph.Contains("WildlifeLungeArrow", StringComparison.Ordinal) &&
                   telegraph.Contains("Rectangle", StringComparison.Ordinal),
                "telegraph rendering must provide persistent shape cues in addition to color");
        });

        RuntimeLog.Shutdown();

        if (Failures.Count > 0)
            throw new InvalidOperationException(
                $"Cowbania.Host.Tests: FAIL ({Failures.Count}){Environment.NewLine}" +
                string.Join(Environment.NewLine, Failures.Select(failure => $"- {failure}")));

        Console.WriteLine("Cowbania.Host.Tests: PASS");
    }

    static string RuntimeLogPath => Path.Combine(AppContext.BaseDirectory, "Cowbania.Host.log");
    static string StartupLogPath => Path.Combine(AppContext.BaseDirectory, "Cowbania.Host.startup.log");

    static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Cowbania.sln")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Cannot locate the repository root.");
    }

    static void DeleteDiagnosticLogs()
    {
        File.Delete(RuntimeLogPath);
        File.Delete(StartupLogPath);
    }

    static string ReadRuntimeLog()
    {
        RuntimeLog.Flush();
        return File.ReadAllText(RuntimeLogPath);
    }

    static string ReadSource(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));

    static IEnumerable<string> AllAnimationAssetKeys() =>
        FrontierAnimationCatalog.PlayerClips.Values
            .Concat(FrontierAnimationCatalog.BanditClips.Values)
            .Concat(FrontierAnimationCatalog.WildlifeClips.Values)
            .Concat(FrontierAnimationCatalog.PickupClips.Values)
            .SelectMany(clip => clip.Frames)
            .Select(frame => frame.AssetKey)
            .Distinct(StringComparer.Ordinal);

    static HashSet<string> ExpectedFrontierAssets()
    {
        var assets = AllAnimationAssetKeys().ToHashSet(StringComparer.Ordinal);
        AddNamed(assets, "Terrain", "ground_cap", "ground_body", "platform_left", "platform_middle",
            "platform_right", "timber_support", "stone", "mine_reinforcement");
        AddNamed(assets, "Props", "cactus_0", "cactus_1", "crate", "sign", "checkpoint", "shortcut",
            "transition_gate", "wagon_debris", "mine_timber");
        AddNamed(assets, "Effects", "muzzle_0", "muzzle_1", "muzzle_2", "impact_0", "impact_1", "impact_2",
            "dust_0", "dust_1", "dust_2", "dash_0", "dash_1", "dash_2", "hurt_0", "hurt_1",
            "defeat_0", "defeat_1", "defeat_2", "pickup_0", "pickup_1", "pickup_2", "pickup_3");
        AddNamed(assets, "UI", "heart_full", "heart_empty", "ammo_full", "ammo_empty", "currency",
            "slot_frame", "panel_corner");
        AddNamed(assets, "Background", "hub_far", "hub_mid", "branch_far", "branch_mid");
        return assets;
    }

    static void AddNamed(HashSet<string> assets, string category, params string[] names)
    {
        foreach (var name in names)
            assets.Add($"Frontier/{category}/{name}.png");
    }

    static (int Width, int Height) ExpectedPngSize(string asset)
    {
        if (asset.StartsWith("Frontier/Background/", StringComparison.Ordinal))
            return (256, 144);
        if (new[] { "checkpoint.png", "shortcut.png", "transition_gate.png", "wagon_debris.png", "mine_timber.png" }
            .Any(name => asset.EndsWith($"/{name}", StringComparison.Ordinal)))
            return (32, 32);
        return (16, 16);
    }

    static PngInfo ReadPng(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Assert(bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            $"{path} has an invalid PNG signature");

        var offset = 8;
        var idat = new MemoryStream();
        var width = 0;
        var height = 0;
        byte bitDepth = 0;
        byte colorType = 0;
        byte interlace = 0;
        while (offset + 12 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            var type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
            var data = bytes.AsSpan(offset + 8, length);
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data[..4]);
                height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4));
                bitDepth = data[8];
                colorType = data[9];
                interlace = data[12];
            }
            else if (type == "IDAT")
            {
                idat.Write(data);
            }
            else if (type == "IEND")
            {
                break;
            }
            offset += length + 12;
        }

        Assert(width > 0 && height > 0 && idat.Length > 0, $"{path} is missing required PNG chunks");
        Assert(bitDepth == 8 && colorType == 6 && interlace == 0,
            $"{path} must be non-interlaced 8-bit RGBA before alpha validation");

        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        var scanlines = raw.ToArray();
        var stride = width * 4;
        Assert(scanlines.Length == (stride + 1) * height, $"{path} has an unexpected RGBA scanline length");
        var previous = new byte[stride];
        var current = new byte[stride];
        byte minimumAlpha = byte.MaxValue;
        byte maximumAlpha = byte.MinValue;
        for (var row = 0; row < height; row++)
        {
            var rowOffset = row * (stride + 1);
            var filter = scanlines[rowOffset];
            for (var column = 0; column < stride; column++)
            {
                var encoded = scanlines[rowOffset + 1 + column];
                var left = column >= 4 ? current[column - 4] : (byte)0;
                var up = previous[column];
                var upperLeft = column >= 4 ? previous[column - 4] : (byte)0;
                current[column] = filter switch
                {
                    0 => encoded,
                    1 => unchecked((byte)(encoded + left)),
                    2 => unchecked((byte)(encoded + up)),
                    3 => unchecked((byte)(encoded + ((left + up) >> 1))),
                    4 => unchecked((byte)(encoded + Paeth(left, up, upperLeft))),
                    _ => throw new InvalidDataException($"{path} uses unsupported PNG filter {filter}")
                };
            }
            for (var alpha = 3; alpha < stride; alpha += 4)
            {
                minimumAlpha = Math.Min(minimumAlpha, current[alpha]);
                maximumAlpha = Math.Max(maximumAlpha, current[alpha]);
            }
            (previous, current) = (current, previous);
            Array.Clear(current);
        }
        return new PngInfo(width, height, bitDepth, colorType, interlace, minimumAlpha, maximumAlpha);
    }

    static byte Paeth(byte left, byte up, byte upperLeft)
    {
        var prediction = left + up - upperLeft;
        var leftDistance = Math.Abs(prediction - left);
        var upDistance = Math.Abs(prediction - up);
        var upperLeftDistance = Math.Abs(prediction - upperLeft);
        return leftDistance <= upDistance && leftDistance <= upperLeftDistance
            ? left
            : upDistance <= upperLeftDistance ? up : upperLeft;
    }

    static void AdvancePresentation(
        PresentationAnimationClock clock,
        EnemyState snapshot,
        float elapsedSeconds,
        bool paused,
        bool completed)
    {
        if (!paused && !completed)
            clock.Advance(elapsedSeconds, snapshot);
    }

    static string MethodBody(string source, string methodName)
    {
        var declaration = System.Text.RegularExpressions.Regex.Match(
            source,
            $@"(?m)^\s+(?:private|protected|public)\s+[^\r\n]*\b{System.Text.RegularExpressions.Regex.Escape(methodName)}\s*\(");
        Assert(declaration.Success, $"cannot find method {methodName}");
        var nextDeclaration = System.Text.RegularExpressions.Regex.Match(
            source[(declaration.Index + declaration.Length)..],
            @"(?m)^\s+(?:private|protected|public)\s+");
        var end = nextDeclaration.Success
            ? declaration.Index + declaration.Length + nextDeclaration.Index
            : source.Length;
        return source[declaration.Index..end];
    }

    static void AssertInOrder(string source, params string[] markers)
    {
        var previous = -1;
        foreach (var marker in markers)
        {
            var index = source.IndexOf(marker, previous + 1, StringComparison.Ordinal);
            Assert(index > previous, $"render marker '{marker}' is missing or out of depth order");
            previous = index;
        }
    }

    static int Count(string value, string marker)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += marker.Length;
        }

        return count;
    }

    static void Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine($"[PASS] {name}");
        }
        catch (Exception exception)
        {
            var failure = $"{name}: {exception.Message}";
            Failures.Add(failure);
            Console.WriteLine($"[FAIL] {failure}");
        }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    static TException AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }

    static byte[] CreatePcmWav(byte[] pcmData, int sampleRate, short channels)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + pcmData.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(pcmData.Length);
        writer.Write(pcmData);
        return stream.ToArray();
    }

    sealed class ThrowingPlayback : IAudioPlayback
    {
        public bool Play(float volume, float pitch, float pan) =>
            throw new InvalidOperationException("test playback failure");
    }

    sealed class RecordingPlayback(bool result = true) : IAudioPlayback
    {
        public int PlayCount { get; private set; }

        public bool Play(float volume, float pitch, float pan)
        {
            PlayCount++;
            return result;
        }
    }

    sealed class RecordingLoader(IAudioPlayback playback) : IAudioEffectLoader
    {
        public int LoadCount { get; private set; }

        public IAudioPlayback Load(string path)
        {
            LoadCount++;
            return playback;
        }
    }

    sealed class ThrowingLoader : IAudioEffectLoader
    {
        public int LoadCount { get; private set; }

        public IAudioPlayback Load(string path)
        {
            LoadCount++;
            throw new InvalidDataException("test managed decode failure");
        }
    }

    readonly record struct PngInfo(
        int Width,
        int Height,
        byte BitDepth,
        byte ColorType,
        byte InterlaceMethod,
        byte MinimumAlpha,
        byte MaximumAlpha);
}
