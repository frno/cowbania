namespace Cowbania.Host.Tests.Harness;

internal static class TestInventory
{
    public static readonly string[] ExpectedNames =
    [
        "fatal reporting deduplicates only the same exception reference",
        "aggregate and unobserved task failures include every detail and are observed",
        "fatal reports flush runtime and startup sinks",
        "audio playback failure has a terminal failure boundary",
        "audio playback exception logs an event-only failure boundary",
        "managed WAV decoding accepts PCM without native file decoding",
        "bundled jump audio is valid managed PCM",
        "managed WAV decoding rejects invalid input",
        "successful audio load is cached and played",
        "unsuccessful playback disables retries and falls back to silence",
        "decode failure disables retries and falls back to silence",
        "music start is a no-op when the file is missing",
        "music start loads and plays once, ignoring later starts",
        "music decode failure disables playback without throwing",
        "music stop is safe before start and forwards to playback after start",
        "Frontier runtime has no placeholder asset references",
        "Frontier manifest and PNG inventory are complete",
        "Frontier PNGs have valid RGBA signatures dimensions and transparency",
        "Frontier actor and pickup mappings are distinct",
        "presentation clocks are deterministic and gated by pause or completion",
        "terrain tiles keep integer scale and clip partial solid edges",
        "projectiles have persistent visuals distinct from transient effects",
        "every accepted repeated shot restarts the one-shot animation",
        "reload-completion shot signal drives host feedback despite ammo increase",
        "reload gameplay and presentation complete on the same update",
        "defeat effects play three frames once and stop",
        "Frontier renderer preserves sampling geometry depth and snapshot contracts",
        "Frontier output copy and explicit missing asset behavior are enforced",
        "HUD and combat telegraphs include non-color identity cues",
    ];

    public static void Validate(IReadOnlyList<TestCase> cases)
    {
        AssertEx.Equal(ExpectedNames.Length, cases.Count, "registered test count");
        AssertEx.Equal(ExpectedNames.Length, cases.Select(test => test.Name).Distinct(StringComparer.Ordinal).Count(),
            "registered test names must be unique");
        AssertEx.SequenceEqual(
            ExpectedNames.Order(StringComparer.Ordinal),
            cases.Select(test => test.Name).Order(StringComparer.Ordinal),
            "registered test inventory");
    }
}
