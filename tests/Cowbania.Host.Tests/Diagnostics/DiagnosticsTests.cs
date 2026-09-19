namespace Cowbania.Host.Tests.Diagnostics;

internal static class DiagnosticsTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("fatal reporting deduplicates only the same exception reference", () =>
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
            yield return new TestCase("aggregate and unobserved task failures include every detail and are observed", () =>
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
            yield return new TestCase("fatal reports flush runtime and startup sinks", () =>
            {
                RuntimeLog.ReportProcessException(
                                "host-test-flush",
                                new InvalidOperationException("flush-durability-marker"),
                                isTerminating: true);

                            Assert(ReadRuntimeLog().Contains("flush-durability-marker"), "runtime sink is durable after report returns");
                            Assert(File.ReadAllText(StartupLogPath).Contains("flush-durability-marker"),
                                "startup fallback sink is durable after report returns");
            });
        }
    }
}
