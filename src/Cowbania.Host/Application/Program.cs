using Cowbania.Host.Diagnostics;

namespace Cowbania.Host.Application;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        StartupDiagnostics.Mark("process entry");
        FatalExceptionHandlers.Install();
        StartupDiagnostics.Mark("fatal exception handlers installed");
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        StartupDiagnostics.Mark("current directory set");
        StartupDiagnostics.Mark(RuntimeLog.Initialize()
            ? $"log4net ready: {RuntimeLog.LogPath}"
            : $"log4net initialization failed (continuing without runtime log): {RuntimeLog.InitializationError}");

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
    }
}
