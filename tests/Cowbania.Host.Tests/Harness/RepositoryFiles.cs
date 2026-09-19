namespace Cowbania.Host.Tests.Harness;

internal static class RepositoryFiles
{
    public static string RuntimeLogPath => Path.Combine(AppContext.BaseDirectory, "Cowbania.Host.log");
    public static string StartupLogPath => Path.Combine(AppContext.BaseDirectory, "Cowbania.Host.startup.log");

    public static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Cowbania.sln")))
                return directory.FullName;
        throw new InvalidOperationException("Cannot locate the repository root.");
    }

    public static void DeleteDiagnosticLogs()
    {
        File.Delete(RuntimeLogPath);
        File.Delete(StartupLogPath);
    }

    public static string ReadRuntimeLog()
    {
        RuntimeLog.Flush();
        return File.ReadAllText(RuntimeLogPath);
    }

    public static string ReadSource(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
