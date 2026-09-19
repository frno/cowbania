namespace Cowbania.Host.Tests.Presentation;

internal static class PresentationTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("presentation clocks are deterministic and gated by pause or completion", () =>
            {
                var first = new PresentationAnimationClock();
                            var second = new PresentationAnimationClock();
                            var enemy = new EnemyState(
                                "bandit-test", EnemyArchetype.Bandit, EnemyBehaviorState.Attack, EnemyAttackPhase.Telegraph,
                                new System.Numerics.Vector2(100, 480), System.Numerics.Vector2.Zero, 1, 2, true, 0.5f, 0.25f);
                            foreach (var dt in new[] { 0.01f, 0.07f, 0.13f, 0.02f })
                            {
                                first.Advance(dt, enemy);
                                second.Advance(dt, enemy);
                            }
                            Assert(first.CurrentFrame() == second.CurrentFrame() &&
                                   first.CurrentFrameIndex == second.CurrentFrameIndex,
                                "equivalent snapshots and elapsed-time sequences must select the same presentation frame");

                            var beforeGate = first.CurrentFrameIndex;
                            var paused = new GameWorld();
                            paused.Update(new InputFrame(0, false, false, default, false, false, false, true), 0);
                            var completed = new GameWorld();
                            typeof(GameWorld).GetProperty(nameof(GameWorld.Completed))!.SetValue(completed, true);
                            var signals = default(FrameFeedbackSnapshot);
                            if (signals.SimulationActive(paused))
                                first.Advance(1f, enemy);
                            if (signals.SimulationActive(completed))
                                first.Advance(1f, enemy);
                            Assert(first.CurrentFrameIndex == beforeGate,
                                "paused and completed presentation gates must not advance clocks");
                            Assert(!signals.SimulationActive(paused) && !signals.SimulationActive(completed),
                                "the extracted frame signal gate rejects paused and completed worlds");
            });
            yield return new TestCase("every accepted repeated shot restarts the one-shot animation", () =>
            {
                Assert(PlayerAnimationRestart.ShouldReset(
                                    PresentationAnimationState.Shoot,
                                    PresentationAnimationState.Shoot,
                                    acceptedPlayerShot: true),
                                "a second accepted shot must restart Shoot even when the selected state is unchanged");
                            Assert(!PlayerAnimationRestart.ShouldReset(
                                    PresentationAnimationState.Shoot,
                                    PresentationAnimationState.Shoot,
                                    acceptedPlayerShot: false),
                                "held fire without an ammo decrement must not restart the animation");
                            Assert(!PlayerAnimationRestart.ShouldReset(
                                    PresentationAnimationState.Hurt,
                                    PresentationAnimationState.Hurt,
                                    acceptedPlayerShot: true),
                                "an accepted shot must not override a higher-precedence selected state");

                            var game = new GameWorld();
                            game.Update(new InputFrame(0, false, false, System.Numerics.Vector2.UnitX, true, false, false, false), 0);
                            Assert(game.PlayerShotAcceptedThisUpdate,
                                "shooting feedback consumes the deterministic core acceptance signal");
            });
            yield return new TestCase("reload gameplay and presentation complete on the same update", () =>
            {
                const float startingUpdateSeconds = 0.08f;
                            var game = new GameWorld();
                            var reloadAnimation = FrontierAnimationCatalog.For(PresentationAnimationState.Reload);
                            var animationClock = new AnimationClock();

                            game.Update(new InputFrame(0, false, false, System.Numerics.Vector2.UnitX, true, false, false, false), 0f);
                            game.Update(new InputFrame(0, false, false, System.Numerics.Vector2.UnitX, false, true, false, false),
                                startingUpdateSeconds);
                            animationClock.Advance(startingUpdateSeconds, reloadAnimation);

                            Assert(game.IsReloading && !animationClock.IsComplete,
                                "a reload begun on a nonzero update remains active in gameplay and presentation");

                            var completingUpdateSeconds = GameWorld.ReloadDuration - startingUpdateSeconds + 0.0001f;
                            game.Update(default, completingUpdateSeconds);
                            animationClock.Advance(completingUpdateSeconds, reloadAnimation);

                            Assert(!game.IsReloading && game.Ammo == 6 && animationClock.IsComplete,
                                "gameplay refill and the Frontier reload one-shot complete on the same update");
            });
        }
    }
}
