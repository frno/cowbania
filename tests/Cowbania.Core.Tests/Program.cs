using Cowbania.Core.Tests.Harness;

var cases = new IEnumerable<TestCase>[]
{
    Cowbania.Core.Tests.Animation.AnimationTests.Cases,
    Cowbania.Core.Tests.Presentation.PresentationTests.Cases,
    Cowbania.Core.Tests.Combat.CombatTests.Cases,
    Cowbania.Core.Tests.Player.Movement.PlayerMovementTests.Cases,
    Cowbania.Core.Tests.Diagnostics.DiagnosticsTests.Cases,
    Cowbania.Core.Tests.World.WorldTests.Cases,
    Cowbania.Core.Tests.Determinism.DeterminismTests.Cases,
    Cowbania.Core.Tests.Enemies.EnemiesTests.Cases,
    Cowbania.Core.Tests.Pickups.PickupsTests.Cases
}.SelectMany(cases => cases).ToArray();

TestInventory.Validate(cases);
TestRunner.Run(cases);
