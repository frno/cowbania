using System.Runtime.InteropServices;
using System.Diagnostics;

StartupDiagnostics.Mark("process entry");
FatalExceptionHandlers.Install();
StartupDiagnostics.Mark("fatal exception handlers installed");
Directory.SetCurrentDirectory(AppContext.BaseDirectory);
StartupDiagnostics.Mark("current directory set");
if (RuntimeLog.Initialize())
{
    StartupDiagnostics.Mark($"log4net ready: {RuntimeLog.LogPath}");
}
else
{
    StartupDiagnostics.Mark($"log4net initialization failed (continuing without runtime log): {RuntimeLog.InitializationError}");
}

try
{
    StartupDiagnostics.Mark("constructing CowbaniaGame");
    using var game = new CowbaniaGame();
    StartupDiagnostics.Mark("CowbaniaGame constructed; entering Game.Run");
    game.Run();
    StartupDiagnostics.Mark("Game.Run returned");
}
catch (Exception exception)
{
    RuntimeLog.ReportProcessException("Game.Run", exception, isTerminating: true);
    StartupDiagnostics.Report(exception);
    throw;
}
finally
{
    StartupDiagnostics.Mark("process exit");
    RuntimeLog.Shutdown();
}

internal static class FatalExceptionHandlers
{
    private static int installed;

    public static void Install()
    {
        if (Interlocked.Exchange(ref installed, 1) != 0)
            return;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            RuntimeLog.ReportProcessException("AppDomain.UnhandledException", args.ExceptionObject, args.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, args) =>
            ReportUnobservedTaskException(args);
    }

    internal static void ReportUnobservedTaskException(UnobservedTaskExceptionEventArgs args)
    {
        RuntimeLog.ReportProcessException("TaskScheduler.UnobservedTaskException", args.Exception, isTerminating: false);
        args.SetObserved();
    }
}

internal static class StartupDiagnostics
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly object Sync = new();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(nint hWnd, string text, string caption, uint type);

    public static void Mark(string message)
    {
        var line = $"{DateTimeOffset.Now:O} +{Clock.Elapsed.TotalMilliseconds,10:F1} ms {message}{Environment.NewLine}";
        var logPath = Path.Combine(AppContext.BaseDirectory, "Cowbania.Host.startup.log");

        try
        {
            lock (Sync)
            {
                using var writer = new StreamWriter(logPath, append: true);
                writer.Write(line);
                writer.Flush();
            }
        }
        catch
        {
            // Startup diagnostics must not mask the original startup path.
        }

        RuntimeLog.Info($"startup +{Clock.Elapsed.TotalMilliseconds:F1}ms {message}");
    }

    public static void Report(Exception exception)
    {
        var message = $"Cowbania could not start.\n\n{exception}";

        try
        {
            if (OperatingSystem.IsWindows())
                MessageBox(0, message, "Cowbania startup error", 0x10);
            else
                Console.Error.WriteLine(message);
        }
        catch
        {
            Console.Error.WriteLine(message);
        }
    }

    public static void RecordFatal(string details)
    {
        WriteLine($"fatal exception:{Environment.NewLine}{details}");
    }

    private static void WriteLine(string message)
    {
        var line = $"{DateTimeOffset.Now:O} +{Clock.Elapsed.TotalMilliseconds,10:F1} ms {message}{Environment.NewLine}";
        var logPath = Path.Combine(AppContext.BaseDirectory, "Cowbania.Host.startup.log");

        try
        {
            lock (Sync)
            {
                using var writer = new StreamWriter(logPath, append: true);
                writer.Write(line);
                writer.Flush();
            }
        }
        catch
        {
            // Startup diagnostics must not mask the original startup path.
        }
    }
}
