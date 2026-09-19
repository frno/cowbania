using System.Collections.Concurrent;
using Cowbania.Core;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Layout;
using log4net.Repository.Hierarchy;

internal static class RuntimeLog
{
    public static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "Cowbania.Host.log");

    private static ILog? logger;
    private static readonly ConcurrentDictionary<Exception, byte> reportedFatalExceptions =
        new(ReferenceEqualityComparer.Instance);

    public static Exception? InitializationError { get; private set; }
    public static bool IsInitialized => logger is not null;

    public static bool Initialize()
    {
        try
        {
            var layout = new PatternLayout
            {
                ConversionPattern = "%date{ISO8601} %-5level [thread:%thread] %message%newline"
            };
            layout.ActivateOptions();

            var appender = new RollingFileAppender
            {
                File = LogPath,
                AppendToFile = true,
                ImmediateFlush = true,
                RollingStyle = RollingFileAppender.RollingMode.Size,
                MaximumFileSize = "5MB",
                MaxSizeRollBackups = 3,
                StaticLogFileName = true,
                LockingModel = new FileAppender.MinimalLock(),
                Layout = layout
            };
            appender.ActivateOptions();

            var repository = (Hierarchy)LogManager.GetRepository();
            repository.Root.Level = log4net.Core.Level.Info;
            BasicConfigurator.Configure(repository, appender);
            logger = LogManager.GetLogger(typeof(RuntimeLog));
            logger.Info($"runtime log initialized path=\"{LogPath}\"");
            return true;
        }
        catch (Exception exception)
        {
            InitializationError = exception;
            Console.Error.WriteLine($"Cowbania logging initialization failed: {exception}");
            return false;
        }
    }

    public static void Info(string message) => SafeWrite(log => log.Info(message));
    public static void Warn(string message) => SafeWrite(log => log.Warn(message));
    public static void Error(string message, Exception exception) => SafeWrite(log => log.Error(message, exception));
    public static void Fatal(string message, Exception exception) => SafeWrite(log => log.Fatal(message, exception));

    public static void ReportProcessException(string source, object? exceptionObject, bool isTerminating)
    {
        var details = FatalExceptionFormatter.Format(source, exceptionObject, isTerminating);
        if (exceptionObject is Exception exception)
        {
            if (!reportedFatalExceptions.TryAdd(exception, 0))
                return;

            SafeWrite(log => log.Fatal(details, exception));
        }
        else
        {
            SafeWrite(log => log.Fatal(details));
        }

        StartupDiagnostics.RecordFatal(details);
        Console.Error.WriteLine(details);
        Flush();
    }

    public static void Flush()
    {
        try
        {
            LogManager.Flush(2000);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Cowbania logging flush failed: {exception}");
        }
    }

    public static void Shutdown()
    {
        try
        {
            logger?.Info("runtime log shutdown");
            Flush();
            LogManager.Shutdown();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Cowbania logging shutdown failed: {exception}");
        }
    }

    private static void SafeWrite(Action<ILog> write)
    {
        try
        {
            if (logger is not null)
                write(logger);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Cowbania logging write failed: {exception.Message}");
        }
    }

}
