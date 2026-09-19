namespace Cowbania.Host.Tests.Input;

internal static class InputTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("reload-completion shot signal drives host feedback despite ammo increase", () =>
            {
                var game = new GameWorld();
                            for (var shot = 0; shot < 6; shot++)
                            {
                                game.Update(new InputFrame(
                                        0, false, false, System.Numerics.Vector2.UnitX, true, false, false, false),
                                    shot == 0 ? 0f : GameWorld.FireDelay);
                            }

                            game.Update(new InputFrame(
                                    0, false, false, System.Numerics.Vector2.UnitX, true, false, false, false),
                                GameWorld.ReloadDuration - GameWorld.FireDelay);

                            Assert(game.Ammo == 5 && game.PlayerShotAcceptedThisUpdate,
                                "core exposes the accepted shot when reload completion raises ammo from empty to five");
                            Assert(PlayerAnimationRestart.ShouldReset(
                                    PresentationAnimationState.Shoot,
                                    PresentationAnimationState.Shoot,
                                    game.PlayerShotAcceptedThisUpdate),
                                "the host restarts Shoot from the explicit signal even when Shoot is already selected");
            });
        }
    }
}
