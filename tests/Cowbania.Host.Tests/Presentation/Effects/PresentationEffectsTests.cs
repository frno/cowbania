namespace Cowbania.Host.Tests.Presentation.Effects;

internal static class PresentationEffectsTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("defeat effects play three frames once and stop", () =>
            {
                var clock = new BoundedEffectClock(3, 6f);
                            Assert(clock.IsVisible && clock.CurrentFrameIndex == 0, "defeat starts on frame zero");
                            clock.Advance(0.17f);
                            Assert(clock.IsVisible && clock.CurrentFrameIndex == 1, "defeat advances to frame one");
                            clock.Advance(0.17f);
                            Assert(clock.IsVisible && clock.CurrentFrameIndex == 2, "defeat advances to frame two");
                            clock.Advance(0.17f);
                            Assert(!clock.IsVisible && clock.CurrentFrameIndex == 2,
                                "defeat stops after its final frame instead of wrapping");
                            clock.Advance(10f);
                            Assert(!clock.IsVisible && clock.CurrentFrameIndex == 2,
                                "completed defeat effects remain bounded");

                            var timelineSource = ReadSource(
                                FindRepositoryRoot(), "src", "Cowbania.Host", "Presentation", "PresentationTimeline.cs");
                            var effects = MethodBody(
                                ReadSource(FindRepositoryRoot(), "src", "Cowbania.Host", "Presentation", "Effects", "EffectRenderer.cs"),
                                "DrawEnemy");
                            Assert(timelineSource.Contains(
                                       "Dictionary<string, BoundedEffectClock> defeatClocks",
                                       StringComparison.Ordinal) &&
                                   timelineSource.Contains("defeatClocks.Clear()", StringComparison.Ordinal),
                                "defeat clocks are keyed by stable enemy ID and reset with the encounter");
                            Assert(effects.Contains("clock?.IsVisible == true", StringComparison.Ordinal) &&
                                   !effects.Contains("FixedFrame", StringComparison.Ordinal),
                                "defeat rendering stops after the bounded clock completes");
            });
        }
    }
}
