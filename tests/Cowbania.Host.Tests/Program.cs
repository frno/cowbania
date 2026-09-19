using Cowbania.Host.Tests.Harness;

DeleteDiagnosticLogs();
if (!RuntimeLog.Initialize())
    throw new InvalidOperationException($"runtime logger initializes: {RuntimeLog.InitializationError}");

var cases = new IEnumerable<TestCase>[]
{
    Cowbania.Host.Tests.Diagnostics.DiagnosticsTests.Cases,
    Cowbania.Host.Tests.Audio.AudioTests.Cases,
    Cowbania.Host.Tests.Presentation.PresentationTests.Cases,
    Cowbania.Host.Tests.Assets.AssetsTests.Cases,
    Cowbania.Host.Tests.Presentation.Rendering.PresentationRenderingTests.Cases,
    Cowbania.Host.Tests.Input.InputTests.Cases,
    Cowbania.Host.Tests.Presentation.Effects.PresentationEffectsTests.Cases,
    Cowbania.Host.Tests.Presentation.Hud.PresentationHudTests.Cases
}.SelectMany(cases => cases).ToArray();

TestInventory.Validate(cases);
try
{
    TestRunner.Run(cases);
}
finally
{
    RuntimeLog.Shutdown();
}
