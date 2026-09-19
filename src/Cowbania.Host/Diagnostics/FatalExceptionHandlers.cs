namespace Cowbania.Host.Diagnostics;

internal static class FatalExceptionHandlers
{
    private static int installed;

    public static void Install()
    {
        if (Interlocked.Exchange(ref installed, 1) != 0)
            return;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            RuntimeLog.ReportProcessException("AppDomain.UnhandledException", args.ExceptionObject, args.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, args) => ReportUnobservedTaskException(args);
    }

    internal static void ReportUnobservedTaskException(UnobservedTaskExceptionEventArgs args)
    {
        RuntimeLog.ReportProcessException("TaskScheduler.UnobservedTaskException", args.Exception, isTerminating: false);
        args.SetObserved();
    }
}
