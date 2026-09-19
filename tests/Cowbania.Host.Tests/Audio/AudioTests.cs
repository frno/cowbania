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
        }
    }
}
