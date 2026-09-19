using System.Diagnostics;
using System.Runtime.InteropServices;

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
            var before = ReadRuntimeLog();
            var audio = new AudioEventBus(new Dictionary<AudioEvent, IAudioPlayback>
            {
                [AudioEvent.Jump] = new ThrowingPlayback()
            });

            audio.Play(AudioEvent.Jump);

            var log = ReadRuntimeLog();
            Assert(log.Contains("audio playback begin event=Jump"), "playback begin is logged");
            Assert(log.Contains("audio playback failure event=Jump"), "playback failure is logged");
            Assert(log.Contains("test playback failure"), "playback failure retains exception details");
            Assert(Count(log, "audio playback") - Count(before, "audio playback") == 3,
                "one playback request writes only request, begin, and failure boundaries");
        });

        RuntimeLog.Shutdown();

        Run("fatal subprocess exits nonzero with durable diagnostics", () =>
        {
            var hostDirectory = Path.Combine(FindRepositoryRoot(), "src", "Cowbania.Host", "bin", "Debug", "net10.0");
            var hostLog = Path.Combine(hostDirectory, "Cowbania.Host.log");
            var startupLog = Path.Combine(hostDirectory, "Cowbania.Host.startup.log");
            File.Delete(hostLog);
            File.Delete(startupLog);

            using var process = Process.Start(new ProcessStartInfo("dotnet", $"\"{Path.Combine(hostDirectory, "Cowbania.Host.dll")}\" --fatal-probe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = hostDirectory
            }) ?? throw new InvalidOperationException("Unable to start fatal probe.");
            DismissFatalDialogIfNeeded(process);
            Assert(process.WaitForExit(15000), "fatal probe exits promptly");
            Assert(process.ExitCode != 0, "fatal probe retains a nonzero fatal exit");
            Assert(File.ReadAllText(hostLog).Contains("controlled fatal probe"),
                "fatal probe flushes the runtime sink before exit");
            Assert(File.ReadAllText(startupLog).Contains("controlled fatal probe"),
                "fatal probe retains the startup fallback before exit");
            Assert(Count(File.ReadAllText(startupLog), "controlled fatal probe") == 1,
                "fatal probe writes exactly one startup fatal record for its exception instance");
        });

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

    static void DismissFatalDialogIfNeeded(Process process)
    {
        if (!OperatingSystem.IsWindows() || process.WaitForExit(1000))
            return;

        var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 10;
        while (!process.HasExited && Stopwatch.GetTimestamp() < deadline)
        {
            nint window = 0;
            EnumWindows((candidate, _) =>
            {
                GetWindowThreadProcessId(candidate, out var processId);
                if (processId == process.Id)
                {
                    window = candidate;
                    return false;
                }

                return true;
            }, 0);

            if (window != 0)
            {
                PostMessage(window, 0x0010, 0, 0);
                return;
            }

            Thread.Sleep(50);
        }
    }

    private delegate bool EnumWindowsProc(nint window, nint parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out int processId);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);

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

    sealed class ThrowingPlayback : IAudioPlayback
    {
        public bool Play(float volume, float pitch, float pan) =>
            throw new InvalidOperationException("test playback failure");
    }
}
