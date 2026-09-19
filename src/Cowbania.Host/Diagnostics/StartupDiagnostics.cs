using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Cowbania.Host.Diagnostics;

internal static class StartupDiagnostics
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly object Sync = new();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(nint hWnd, string text, string caption, uint type);

    public static void Mark(string message)
    {
        WriteLine(message);
        RuntimeLog.Info($"startup +{Clock.Elapsed.TotalMilliseconds:F1}ms {message}");
    }

    public static void Report(Exception exception)
    {
        var message = $"Cowbania could not start.\n\n{exception}";
        try
        {
            if (OperatingSystem.IsWindows()) MessageBox(0, message, "Cowbania startup error", 0x10);
            else Console.Error.WriteLine(message);
        }
        catch
        {
            Console.Error.WriteLine(message);
        }
    }

    public static void RecordFatal(string details) =>
        WriteLine($"fatal exception:{Environment.NewLine}{details}");

    private static void WriteLine(string message)
    {
        var line = $"{DateTimeOffset.Now:O} +{Clock.Elapsed.TotalMilliseconds,10:F1} ms {message}{Environment.NewLine}";
        var path = Path.Combine(AppContext.BaseDirectory, "Cowbania.Host.startup.log");
        try
        {
            lock (Sync)
            {
                using var writer = new StreamWriter(path, append: true);
                writer.Write(line);
                writer.Flush();
            }
        }
        catch
        {
            // Startup diagnostics cannot mask the original startup path.
        }
    }
}
