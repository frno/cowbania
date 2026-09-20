namespace Cowbania.Host.Tests.Audio;

internal static class AudioTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("audio playback failure has a terminal failure boundary", () =>
            {
                new AudioEventBus().Play(AudioEvent.Jump);
                            var log = ReadRuntimeLog();
                            Assert(log.Contains("audio playback request event=Jump"), "playback request is logged");
                            Assert(log.Contains("audio playback failure event=Jump stage=decode-or-unavailable"),
                                "unavailable playback emits a failure boundary");
            });
            yield return new TestCase("audio playback exception logs an event-only failure boundary", () =>
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
            yield return new TestCase("managed WAV decoding accepts PCM without native file decoding", () =>
            {
                var wav = ManagedPcmWav.Read(CreatePcmWav([0, 1, 2, 3], sampleRate: 22050, channels: 1));

                            Assert(wav.SampleRate == 22050, "managed decoding preserves the sample rate");
                            Assert(wav.Channels == Microsoft.Xna.Framework.Audio.AudioChannels.Mono,
                                "managed decoding preserves the channel layout");
                            Assert(wav.PcmData.SequenceEqual(new byte[] { 0, 1, 2, 3 }),
                                "managed decoding extracts the PCM payload");
            });
            yield return new TestCase("bundled jump audio is valid managed PCM", () =>
            {
                var jumpPath = Path.Combine(FindRepositoryRoot(), "Assets", "Audio", "SFX_Jump.wav");
                            var wav = ManagedPcmWav.Read(File.ReadAllBytes(jumpPath));

                            Assert(wav.SampleRate == 44100, "jump audio preserves its authored sample rate");
                            Assert(wav.Channels == Microsoft.Xna.Framework.Audio.AudioChannels.Mono,
                                "jump audio preserves its authored mono layout");
                            Assert(wav.PcmData.Length > 0, "jump audio contains PCM samples");
            });
            yield return new TestCase("armadillo blocked-shot feedback routes to valid armor audio", () =>
            {
                var armorPath = Path.Combine(FindRepositoryRoot(), "Assets", "Audio", "SFX_ArmorRicochet.wav");
                var wav = ManagedPcmWav.Read(File.ReadAllBytes(armorPath));
                Assert(wav.SampleRate == 44100, "armor ricochet audio preserves its authored sample rate");
                Assert(wav.Channels == Microsoft.Xna.Framework.Audio.AudioChannels.Mono,
                    "armor ricochet audio preserves its authored mono layout");
                Assert(wav.PcmData.Length > 0, "armor ricochet audio contains PCM samples");

                var playback = new RecordingPlayback();
                var audio = new AudioEventBus(new Dictionary<AudioEvent, IAudioPlayback>
                {
                    [AudioEvent.ArmorRicochet] = playback
                });
                var world = new GameWorld();
                var state = typeof(GameWorld).GetField(
                    "state",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(world)!;
                state.GetType().GetField(
                    "ArmadilloShotBlockedThisUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(state, true);
                var signals = new FrameFeedbackSnapshot(
                    default,
                    world.Health,
                    world.CollectedPickupCount,
                    world.IsReloading,
                    world.IsPaused,
                    world.Room,
                    world.ObjectivePhase,
                    world.CheckpointRoom,
                    world.CheckpointPosition,
                    world.Enemies,
                    world.Projectiles);

                new AudioFeedbackRouter(audio).Route(signals, world, 1);

                Assert(playback.PlayCount == 1,
                    "a blocked armadillo shot dispatches the distinctive armor ricochet once");
            });
            yield return new TestCase("managed WAV decoding rejects invalid input", () =>
            {
                var exception = AssertThrows<InvalidDataException>(
                                () => ManagedPcmWav.Read("not a wave"u8),
                                "invalid WAV input fails through the managed decoder");
                            Assert(exception.Message.Contains("RIFF/WAVE"), "managed failure explains the invalid container");
            });
            yield return new TestCase("successful audio load is cached and played", () =>
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
            yield return new TestCase("unsuccessful playback disables retries and falls back to silence", () =>
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
            yield return new TestCase("decode failure disables retries and falls back to silence", () =>
            {
                var path = Path.Combine(AppContext.BaseDirectory, "managed-audio-failure.wav");
                File.WriteAllBytes(path, "invalid"u8.ToArray());
                try
                {
                    var loader = new ThrowingLoader();
                    var audio = new AudioEventBus(null, loader, _ => path, initialized: true);

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
            yield return new TestCase("pending audio initialization keeps sound events responsive", () =>
            {
                var playback = new RecordingPlayback();
                var initialization = new StubAudioInitialization(AudioInitializationState.Pending);
                var audio = new AudioEventBus(
                    new Dictionary<AudioEvent, IAudioPlayback> { [AudioEvent.Jump] = playback },
                    audioInitialization: initialization);

                audio.Play(AudioEvent.Jump);
                Assert(playback.PlayCount == 0, "pending device initialization returns without playing");

                initialization.State = AudioInitializationState.Ready;
                audio.Play(AudioEvent.Jump);
                Assert(playback.PlayCount == 1, "the same event plays after device initialization succeeds");
                Assert(ReadRuntimeLog().Contains("reason=device-initialization-pending fallback=silence"),
                    "temporary silence while initialization is pending is logged");
            });
            yield return new TestCase("failed audio initialization permanently disables sound events", () =>
            {
                var playback = new RecordingPlayback();
                var initialization = new StubAudioInitialization(AudioInitializationState.Failed)
                {
                    Failure = new InvalidOperationException("test device failure")
                };
                var audio = new AudioEventBus(
                    new Dictionary<AudioEvent, IAudioPlayback> { [AudioEvent.Jump] = playback },
                    audioInitialization: initialization);

                audio.Play(AudioEvent.Jump);
                initialization.State = AudioInitializationState.Ready;
                audio.Play(AudioEvent.Jump);

                Assert(playback.PlayCount == 0, "a device initialization failure disables later retries");
                Assert(ReadRuntimeLog().Contains("stage=device-initialization disabled=true fallback=silence"),
                    "device initialization failure records a terminal silent fallback");
            });
            yield return new TestCase("music start is a no-op when the file is missing", () =>
            {
                var loader = new RecordingMusicLoader(new RecordingMusicPlayback());
                var player = new MusicPlayer(loader, _ => Path.Combine(AppContext.BaseDirectory, "no-such-music.wav"));

                player.Start();

                Assert(loader.LoadCount == 0, "a missing music file is never handed to the loader");
                var log = ReadRuntimeLog();
                Assert(log.Contains("background music missing path="), "missing music path is logged");
            });
            yield return new TestCase("music start loads and plays once, ignoring later starts", () =>
            {
                var path = Path.Combine(AppContext.BaseDirectory, "managed-music-success.wav");
                File.WriteAllBytes(path, CreatePcmWav([0, 0], sampleRate: 44100, channels: 1));
                try
                {
                    var playback = new RecordingMusicPlayback();
                    var loader = new RecordingMusicLoader(playback);
                    var player = new MusicPlayer(loader, _ => path);

                    player.Start();
                    player.Start();

                    Assert(loader.LoadCount == 1, "background music loads only once");
                    Assert(playback.PlayCount == 1, "background music plays only once across repeated starts");
                    Assert(playback.LastVolume > 0f, "background music plays at an audible volume");
                    var log = ReadRuntimeLog();
                    Assert(log.Contains("background music load begin path="), "music load begin is logged");
                    Assert(log.Contains("background music playback started"), "music playback start is logged");
                }
                finally
                {
                    File.Delete(path);
                }
            });
            yield return new TestCase("music decode failure disables playback without throwing", () =>
            {
                var path = Path.Combine(AppContext.BaseDirectory, "managed-music-failure.wav");
                File.WriteAllBytes(path, "invalid"u8.ToArray());
                try
                {
                    var loader = new ThrowingMusicLoader();
                    var player = new MusicPlayer(loader, _ => path);

                    player.Start();
                    player.Start();

                    Assert(loader.LoadCount == 1, "a failed music decode is never retried");
                    var log = ReadRuntimeLog();
                    Assert(log.Contains("background music load/playback failure"), "music failure is logged");
                }
                finally
                {
                    File.Delete(path);
                }
            });
            yield return new TestCase("music stop is safe before start and forwards to playback after start", () =>
            {
                var path = Path.Combine(AppContext.BaseDirectory, "managed-music-stop.wav");
                File.WriteAllBytes(path, CreatePcmWav([0, 0], sampleRate: 44100, channels: 1));
                try
                {
                    var playback = new RecordingMusicPlayback();
                    var loader = new RecordingMusicLoader(playback);
                    var player = new MusicPlayer(loader, _ => path);

                    player.Stop();
                    Assert(playback.StopCount == 0, "stop before start does not touch an unstarted playback");

                    player.Start();
                    player.Stop();
                    Assert(playback.StopCount == 1, "stop after start forwards to the underlying playback");
                }
                finally
                {
                    File.Delete(path);
                }
            });
            yield return new TestCase("music waits for audio initialization without blocking startup", () =>
            {
                var path = Path.Combine(AppContext.BaseDirectory, "managed-music-deferred.wav");
                File.WriteAllBytes(path, CreatePcmWav([0, 0], sampleRate: 44100, channels: 1));
                try
                {
                    var initialization = new StubAudioInitialization(AudioInitializationState.Pending);
                    var playback = new RecordingMusicPlayback();
                    var loader = new RecordingMusicLoader(playback);
                    var player = new MusicPlayer(loader, _ => path, initialization);

                    player.Start();
                    player.Update();
                    Assert(loader.LoadCount == 0, "pending initialization does not load music on the game thread");

                    initialization.State = AudioInitializationState.Ready;
                    player.Update();
                    player.Update();
                    Assert(loader.LoadCount == 1, "music loads once after initialization succeeds");
                    Assert(playback.PlayCount == 1, "music plays once after initialization succeeds");
                }
                finally
                {
                    File.Delete(path);
                }
            });
            yield return new TestCase("music stays disabled after audio initialization fails", () =>
            {
                var initialization = new StubAudioInitialization(AudioInitializationState.Pending);
                var loader = new RecordingMusicLoader(new RecordingMusicPlayback());
                var player = new MusicPlayer(loader, _ => "unused.wav", initialization);

                player.Start();
                initialization.State = AudioInitializationState.Failed;
                player.Update();
                initialization.State = AudioInitializationState.Ready;
                player.Update();

                Assert(loader.LoadCount == 0, "failed initialization prevents current and later music loads");
                Assert(ReadRuntimeLog().Contains("background music disabled because audio device initialization failed"),
                    "music records why it selected silence");
            });
        }
    }
}
