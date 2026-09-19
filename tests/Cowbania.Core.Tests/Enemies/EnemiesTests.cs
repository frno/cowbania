namespace Cowbania.Core.Tests.Enemies;

internal static class EnemiesTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("room definitions expose multiple enemy placements", () =>
            {
                foreach (var room in new[] { RoomCatalog.Hub, RoomCatalog.Branch })
                            {
                                Assert(room.EnemySpawns.Length >= 2, $"{room.Name} defines multiple enemy placements");
                                Assert(room.EnemySpawns.Distinct().Count() == room.EnemySpawns.Length,
                                    $"{room.Name} enemy placements are distinct");
                            }
            });
            yield return new TestCase("game world creates enemies at the current room placements", () =>
            {
                var game = new GameWorld();

                            Assert(game.Enemies.Count == RoomCatalog.Hub.EnemySpawns.Length,
                                "hub creates one enemy for each configured placement");
                            Assert(game.Enemies.Select(enemy => enemy.Position).SequenceEqual(RoomCatalog.Hub.EnemySpawns),
                                "hub enemies start at the authored placements");

                            SetProperty(game, nameof(GameWorld.Room), 1);
                            Assert(game.Enemies.Count == RoomCatalog.Branch.EnemySpawns.Length,
                                "branch creates one enemy for each configured placement");
                            Assert(game.Enemies.Select(enemy => enemy.Position).SequenceEqual(RoomCatalog.Branch.EnemySpawns),
                                "branch enemies start at the authored placements");
            });
            yield return new TestCase("enemy definitions assign stable bandit and wildlife encounters", () =>
            {
                Assert(RoomCatalog.Hub.EnemyDefinitions.Select(enemy => enemy.Archetype)
                                    .SequenceEqual(new[] { EnemyArchetype.Bandit, EnemyArchetype.Wildlife }),
                                "hub slots are assigned bandit then wildlife");
                            Assert(RoomCatalog.Branch.EnemyDefinitions.Select(enemy => enemy.Archetype)
                                    .SequenceEqual(new[] { EnemyArchetype.Wildlife, EnemyArchetype.Bandit }),
                                "branch slots are assigned wildlife then bandit");
                            Assert(RoomCatalog.Hub.EnemyDefinitions.Concat(RoomCatalog.Branch.EnemyDefinitions)
                                    .Select(enemy => enemy.Id).Distinct().Count() == 4,
                                "all authored enemy ids are stable and unique");
                            Assert(RoomCatalog.Hub.EnemyDefinitions.Concat(RoomCatalog.Branch.EnemyDefinitions)
                                    .All(enemy => enemy.HorizontalLeash == 120),
                                "every authored enemy uses the frozen horizontal leash");
            });
            yield return new TestCase("wildlife lunge damage occurs once during active attack", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.Room), RoomCatalog.Branch.Id);
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(600, 480));
                            game.Update(default, 0f);
                            game.Update(default, GameWorld.EnemyNoticeDuration);
                            game.Update(default, 0f);
                            game.Update(default, GameWorld.WildlifeTelegraphDuration);

                            Assert(game.Enemies[0].Archetype == EnemyArchetype.Wildlife &&
                                   game.Enemies[0].AttackPhase == EnemyAttackPhase.Active,
                                "wildlife enters its active lunge after telegraphing");

                            var health = game.Health;
                            game.Update(default, 0.1f);
                            game.Update(default, 0.1f);
                            Assert(game.Health == health - 1,
                                "one active lunge can consume at most one health");
                            game.Update(default, 0.1f);
                            Assert(game.Enemies[0].AttackPhase == EnemyAttackPhase.Recovery,
                                "wildlife completes the active window before recovery");
            });
            yield return new TestCase("enemy movement remains inside authored room and leash bounds", () =>
            {
                var game = new GameWorld();
                            for (var step = 0; step < 120; step++)
                            {
                                SetProperty(game, nameof(GameWorld.PlayerPosition),
                                    new Vector2(step % 2 == 0 ? 12 : 1588, RoomCatalog.Hub.Ground.Y));
                                game.Update(default, 0.05f);

                                foreach (var pair in game.Enemies.Zip(RoomCatalog.Hub.EnemyDefinitions))
                                {
                                    var minimum = MathF.Max(
                                        RoomCatalog.Hub.Bounds.X,
                                        pair.Second.Spawn.X - pair.Second.HorizontalLeash);
                                    var maximum = MathF.Min(
                                        RoomCatalog.Hub.Bounds.Right,
                                        pair.Second.Spawn.X + pair.Second.HorizontalLeash);
                                    Assert(pair.First.Position.X >= minimum && pair.First.Position.X <= maximum,
                                        $"{pair.First.Id} stays inside its authored horizontal leash");
                                    Assert(pair.First.Position.Y == pair.Second.Spawn.Y,
                                        $"{pair.First.Id} remains on authored support geometry");
                                    Assert(pair.First.FacingDirection is -1 or 1,
                                        $"{pair.First.Id} exposes a normalized facing direction");
                                }
                            }
            });
            yield return new TestCase("defeated enemies remain inert", () =>
            {
                var game = new GameWorld();
                            var defeated = game.Enemy with
                            {
                                BehaviorState = EnemyBehaviorState.Defeated,
                                AttackPhase = EnemyAttackPhase.None,
                                Health = 0,
                                Alive = false
                            };
                            SetProperty(game, nameof(GameWorld.Enemy), defeated);
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(280, 480));

                            game.Update(default, GameWorld.EnemyNoticeDuration + GameWorld.BanditTelegraphDuration + 1f);

                            Assert(!game.Enemy.Alive &&
                                   game.Enemy.BehaviorState == EnemyBehaviorState.Defeated &&
                                   game.Enemy.AttackPhase == EnemyAttackPhase.None,
                                "a defeated enemy cannot leave its terminal state");
                            Assert(game.Projectiles.All(projectile => projectile.SourceId != defeated.Id),
                                "a defeated bandit emits no later projectile");
            });
            yield return new TestCase("lethal hostile damage restores the authored encounter", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.Enemy), game.Enemy with
                            {
                                BehaviorState = EnemyBehaviorState.Attack,
                                AttackPhase = EnemyAttackPhase.Telegraph,
                                Position = game.Enemy.Position + new Vector2(40, 0),
                                Health = 1
                            });
                            SetProperty(game, nameof(GameWorld.Health), 1);
                            GetField<List<ProjectileState>>(game, "projectiles").Add(new ProjectileState(
                                game.PlayerPosition + new Vector2(0, -GameWorld.PlayerBodyHeight / 2f),
                                Vector2.Zero,
                                1,
                                ProjectileOwner.Enemy,
                                ProjectileKind.BanditBullet,
                                "lethal-bandit"));

                            game.Update(default, 0f);

                            Assert(game.Health == GameWorld.MaximumHealth && game.Projectiles.Count == 0,
                                "death restores health and clears every projectile");
                            Assert(game.Enemies.All(enemy =>
                                    enemy.Alive &&
                                    enemy.Health == GameWorld.EnemyMaximumHealth &&
                                    enemy.BehaviorState == EnemyBehaviorState.Patrol &&
                                    enemy.AttackPhase == EnemyAttackPhase.None &&
                                    enemy.Position == RoomCatalog.Hub.EnemyDefinitions
                                        .Single(definition => definition.Id == enemy.Id).Spawn),
                                "death restores every destination-room enemy to its authored initial snapshot");
            });
            yield return new TestCase("wildlife active contact deals one hit and invulnerability blocks repeat damage", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.Room), RoomCatalog.Branch.Id);
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(470, 480));
                            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(
                                "branch-wildlife-0",
                                EnemyArchetype.Wildlife,
                                EnemyBehaviorState.Attack,
                                EnemyAttackPhase.Active,
                                new Vector2(470, 480),
                                Vector2.Zero,
                                1,
                                2,
                                true,
                                0,
                                0));
                            SetProperty(game, nameof(GameWorld.Health), 3);
                            SetField(game, "invulnerabilityTimer", 0f);

                            game.Update(default, 0f);
                            Assert(game.Health == 2, "active wildlife contact consumes exactly one health point");

                            SetProperty(game, nameof(GameWorld.Enemy), game.Enemy with { AttackPhase = EnemyAttackPhase.Active });
                            game.Update(default, 0f);
                            Assert(game.Health == 2, "invulnerability ignores repeated strikes during the window");
            });
        }
    }
}
