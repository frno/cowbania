namespace Cowbania.Core.Tests.Diagnostics;

internal static class DiagnosticsTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("fatal exception formatting preserves source, termination, and full details", () =>
            {
                var exception = new AggregateException(
                                "jump audio failed",
                                new InvalidOperationException("decoder unavailable", new ArgumentException("device unavailable")),
                                new InvalidOperationException("playback unavailable"));
                            var details = FatalExceptionFormatter.Format("Game.Run", exception, true);
                            Assert(details.Contains("source=\"Game.Run\""), "fatal details include source");
                            Assert(details.Contains("terminating=True"), "fatal details include termination state");
                            Assert(details.Contains("System.AggregateException: jump audio failed"),
                                "fatal details include full primary exception output");
                            Assert(details.Contains("System.ArgumentException: device unavailable"),
                                "fatal details include inner exception details");
                            Assert(details.Contains("aggregateInner[0].ToString()") &&
                                   details.Contains("aggregateInner[1].ToString()"),
                                "fatal details enumerate every aggregate inner exception");
            });
        }
    }
}
