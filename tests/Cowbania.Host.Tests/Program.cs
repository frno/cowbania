static class Tests
{
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

        RuntimeLog.Shutdown();

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
            throw new InvalidOperationException($"{name}: {exception.Message}", exception);
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
}
