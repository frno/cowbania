namespace Cowbania.Core.Tests.Determinism;

internal static class DeterminismTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("equivalent stage runs produce deterministic gameplay state", () =>
            {
                var first = new GameWorld();
                            var second = new GameWorld();
                            var inputs = Enumerable.Range(0, 90).Select(i => new InputFrame(
                                i % 3 == 0 ? 1 : 0, i == 5, i == 30, i % 11 == 0 ? Vector2.Normalize(new Vector2(1, -1)) : Vector2.UnitX,
                                i % 13 == 0, i == 45, false, false)).ToArray();

                            foreach (var input in inputs)
                            {
                                first.Update(input, 0.016f);
                                second.Update(input, 0.016f);
                            }

                            Assert(first.PlayerPosition == second.PlayerPosition && first.PlayerVelocity == second.PlayerVelocity,
                                "equivalent inputs produce the same player transform");
                            Assert(first.Room == second.Room && first.Health == second.Health && first.Ammo == second.Ammo,
                                "equivalent inputs produce the same gameplay counters and room");
                            Assert(first.Projectiles.SequenceEqual(second.Projectiles), "equivalent inputs produce the same projectiles");
                            Assert(first.Enemies.SequenceEqual(second.Enemies), "equivalent inputs produce the same enemy state");
            });
            yield return new TestCase("release five attack timelines remain deterministic", () =>
            {
                var first = new GameWorld();
                            var second = new GameWorld();
                            SetProperty(first, nameof(GameWorld.PlayerPosition), new Vector2(280, 480));
                            SetProperty(second, nameof(GameWorld.PlayerPosition), new Vector2(280, 480));

                            foreach (var elapsed in new[]
                                     {
                                         0f,
                                         GameWorld.EnemyNoticeDuration,
                                         0f,
                                         GameWorld.BanditTelegraphDuration,
                                         0.05f,
                                         GameWorld.BanditRecoveryDuration,
                                         0.1f
                                     })
                            {
                                first.Update(default, elapsed);
                                second.Update(default, elapsed);
                                Assert(CaptureWorld(first) == CaptureWorld(second),
                                    "equivalent enemy updates preserve snapshots, projectiles, health, and timers");
                            }

                            Assert(first.Enemies.All(enemy =>
                                    enemy.StateTimerNormalized is >= 0 and <= 1 &&
                                    enemy.AttackTimerNormalized is >= 0 and <= 1),
                                "public enemy timers remain normalized throughout the attack timeline");
            });
        }
    }
}
