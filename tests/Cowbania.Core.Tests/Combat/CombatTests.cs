namespace Cowbania.Core.Tests.Combat;

internal static class CombatTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("revolver reload duration is four firing cadences", () =>
            {
                Assert(MathF.Abs(GameWorld.ReloadDuration - 0.56f) < 0.0001f,
                                "reload takes 0.56 seconds, exactly four 0.14-second firing cadences");
            });
            yield return new TestCase("manual reload refills a partially spent cylinder", () =>
            {
                var game = new GameWorld();
                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false), 0f);

                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, true, false, false), 0f);

                            Assert(game.Ammo == 5 && game.IsReloading,
                                "manual reload begins immediately for a partially spent cylinder");
                            Assert(MathF.Abs(GetField<float>(game, "reloadTimer") - GameWorld.ReloadDuration) < 0.0001f,
                                "a zero-duration update leaves the full authoritative reload duration");

                            game.Update(default, GameWorld.ReloadDuration);

                            Assert(game.Ammo == 6 && !game.IsReloading,
                                "manual reload refills exactly to six when its timer completes");
            });
            yield return new TestCase("sixth accepted shot starts automatic reload immediately", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));

                            for (var i = 0; i < 6; i++)
                            {
                                game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false),
                                    i == 0 ? 0f : GameWorld.FireDelay);
                            }

                            Assert(game.Ammo == 0, "six accepted shots consume the full cylinder");
                            Assert(game.Projectiles.Count > 0,
                                "later accepted shots remain active while earlier shots can exhaust their range");
                            Assert(game.Projectiles.All(projectile => projectile.Position.Y == 456), "projectiles spawn from the elevated gun muzzle");
                            Assert(game.PlayerShotAcceptedThisUpdate,
                                "the sixth accepted shot reports acceptance on its update");
                            Assert(game.IsReloading, "the sixth accepted shot starts reload without another input frame");
                            Assert(MathF.Abs(GetField<float>(game, "reloadTimer") -
                                             (GameWorld.ReloadDuration - GameWorld.FireDelay)) < 0.0001f,
                                "automatic reload consumes the same accepted-shot update time as presentation");
            });
            yield return new TestCase("revolver projectiles expire at their maximum travel range", () =>
            {
                var game = new GameWorld();
                SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));
                game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false), 0f);
                var spawn = game.Projectiles.Single().Position;

                game.Update(default,
                    GameWorld.RevolverProjectileRange / GameWorld.RevolverProjectileSpeed - 0.01f);

                Assert(game.Projectiles.Count == 1,
                    "the revolver projectile remains active immediately before its range is exhausted");
                Assert(game.Projectiles[0].RemainingRange > 0,
                    "the projectile snapshot exposes its remaining deterministic travel range");

                game.Update(default, 0.02f);

                Assert(game.Projectiles.Count == 0,
                    "the revolver projectile is removed instead of traveling beyond its maximum range");
                Assert(GameWorld.RevolverProjectileRange == 360f &&
                       Vector2.Distance(spawn, spawn + Vector2.UnitX * GameWorld.RevolverProjectileRange) == 360f,
                    "the configured revolver range is 360 world units");
            });
            yield return new TestCase("revolver endpoint collisions resolve before range expiry", () =>
            {
                var game = new GameWorld();
                var spawn = game.PlayerPosition + GameWorld.PlayerMuzzleOffset +
                            Vector2.UnitX * GameWorld.PlayerMuzzleDistance;
                SetProperty(game, nameof(GameWorld.Enemy),
                    new EnemyState(spawn + Vector2.UnitX * GameWorld.RevolverProjectileRange, 2, true));

                game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false),
                    GameWorld.RevolverProjectileRange / GameWorld.RevolverProjectileSpeed);

                Assert(game.Enemy.Health == 1,
                    "an enemy at the exact revolver endpoint is damaged before the projectile expires");
                Assert(game.Projectiles.Count == 0,
                    "the endpoint hit consumes the revolver projectile");
            });
            yield return new TestCase("enemy projectiles are exempt from the revolver range", () =>
            {
                var game = new GameWorld();
                var projectiles = GetField<List<ProjectileState>>(game, "projectiles");
                projectiles.Add(new ProjectileState(
                    new Vector2(600, 200),
                    Vector2.UnitX * GameWorld.BanditProjectileSpeed,
                    1,
                    ProjectileOwner.Enemy,
                    ProjectileKind.BanditBullet,
                    "range-test-bandit"));

                game.Update(default, 1.3f);

                var hostile = game.Projectiles.Single(projectile => projectile.SourceId == "range-test-bandit");
                Assert(Vector2.Distance(hostile.Position, new Vector2(600, 200)) > GameWorld.RevolverProjectileRange,
                    "enemy projectiles can travel farther than the player's revolver range");
                Assert(float.IsPositiveInfinity(hostile.RemainingRange),
                    "enemy projectiles retain unlimited range until collision or room exit");
            });
            yield return new TestCase("automatic reload blocks firing and refills exactly once", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.Ammo), 1);

                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false), 0f);
                            var projectileCount = game.Projectiles.Count;
                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false),
                                GameWorld.ReloadDuration / 2);

                            Assert(game.Ammo == 0 && game.IsReloading,
                                "the cylinder remains empty before the authoritative reload timer completes");
                            Assert(game.Projectiles.Count == projectileCount,
                                "firing remains blocked while automatic reload is active");
                            Assert(!game.PlayerShotAcceptedThisUpdate,
                                "held fire reports no accepted shot while reload blocks firing");

                            game.Update(default, GameWorld.ReloadDuration / 2 + 0.01f);

                            Assert(game.Ammo == 6 && !game.IsReloading,
                                "reload overshoot clamps cleanly and refills the cylinder exactly to six");
                            Assert(!game.PlayerShotAcceptedThisUpdate,
                                "a refill-only update does not report a player shot");
                            Assert(GetField<float>(game, "reloadTimer") == 0,
                                "the completed reload timer is clamped to zero");

                            game.Update(default, GameWorld.ReloadDuration);
                            Assert(game.Ammo == 6 && !game.IsReloading,
                                "completed reload does not refill again or restart without input");
            });
            yield return new TestCase("held fire shoots on the update that reload completes", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.Ammo), 1);
                            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));
                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, true, false, false), 0f);

                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false),
                                GameWorld.ReloadDuration + 0.01f);

                            Assert(game.Ammo == 5 && !game.IsReloading,
                                "reload completion refills then accepts held fire without a delayed frame");
                            Assert(game.Projectiles.Count == 0,
                                "the immediate post-completion shot can exhaust its range during the same large update");
                            Assert(game.PlayerShotAcceptedThisUpdate,
                                "reload completion reports the accepted held-fire shot despite the net ammo increase");
            });
            yield return new TestCase("player shot acceptance signal is per-update and deterministic", () =>
            {
                var game = new GameWorld();
                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false), 0);

                            Assert(game.Projectiles.Count == 1, "firing creates one projectile");
                            Assert(game.PlayerShotAcceptedThisUpdate, "an ordinary accepted shot sets the per-update signal");
                            Assert(game.Projectiles[0].Position ==
                                game.PlayerPosition + GameWorld.PlayerMuzzleOffset + Vector2.UnitX * GameWorld.PlayerMuzzleDistance,
                                "projectile starts at the gun muzzle");

                            game.Update(default, 0);
                            Assert(!game.PlayerShotAcceptedThisUpdate,
                                "the signal resets at the beginning of the next non-shot update");

                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false), 0);
                            Assert(!game.PlayerShotAcceptedThisUpdate,
                                "fire-cadence blocking leaves the signal false");

                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, true), 0);
                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false), GameWorld.FireDelay);
                            Assert(!game.PlayerShotAcceptedThisUpdate,
                                "paused updates reset and leave the signal false");
            });
            yield return new TestCase("enemy snapshots expose deterministic notice and chase state", () =>
            {
                var game = new GameWorld();
                            var enemy = game.Enemies[0];
                            Assert(enemy.Id == "hub-bandit-0" && enemy.Archetype == EnemyArchetype.Bandit,
                                "the first hub snapshot identifies its authored definition");
                            Assert(enemy.BehaviorState == EnemyBehaviorState.Patrol &&
                                   enemy.AttackPhase == EnemyAttackPhase.None &&
                                   enemy.FacingDirection is -1 or 1,
                                "new enemies begin in patrol with a valid facing");

                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(100, 480));
                            game.Update(default, 0.1f);
                            Assert(game.Enemies[0].BehaviorState == EnemyBehaviorState.Notice,
                                "a nearby player moves the enemy into notice");
                            game.Update(default, GameWorld.EnemyNoticeDuration);
                            Assert(game.Enemies[0].BehaviorState == EnemyBehaviorState.Chase,
                                "notice lasts for the frozen elapsed-time duration");
            });
            yield return new TestCase("bandit attack telegraphs then emits one hostile projectile", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(280, 480));
                            game.Update(default, 0f);
                            game.Update(default, GameWorld.EnemyNoticeDuration);
                            game.Update(default, 0f);

                            Assert(game.Enemies[0].BehaviorState == EnemyBehaviorState.Attack &&
                                   game.Enemies[0].AttackPhase == EnemyAttackPhase.Telegraph,
                                "bandit enters a telegraphed attack only within its ranged window");

                            game.Update(default, GameWorld.BanditTelegraphDuration);
                            var hostile = game.Projectiles.Single(projectile => projectile.Owner == ProjectileOwner.Enemy);
                            Assert(hostile.Kind == ProjectileKind.BanditBullet &&
                                   hostile.SourceId == game.Enemies[0].Id,
                                "bandit projectile identifies its owner kind and source");
                            Assert(hostile.Velocity.Length() == GameWorld.BanditProjectileSpeed,
                                "bandit projectile uses the frozen deterministic speed");

                            game.Update(default, 0.01f);
                            Assert(game.Projectiles.Count(projectile => projectile.Owner == ProjectileOwner.Enemy) == 1,
                                "one bandit attack emits at most one projectile");
            });
            yield return new TestCase("room re-entry resets the encounter and clears every projectile", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.Enemy), game.Enemy with
                            {
                                Position = new Vector2(600, 480),
                                Health = 0,
                                Alive = false,
                                BehaviorState = EnemyBehaviorState.Defeated
                            });
                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false), 0f);
                            Assert(game.Projectiles.Count == 1 && !game.Enemies[0].Alive,
                                "the pre-transition encounter contains transient projectile and defeat state");

                            SetProperty(game, nameof(GameWorld.PlayerPosition),
                                RoomCatalog.Hub.Exit!.Value);
                            game.Update(default, 0f);
                            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Branch.Shortcut);
                            InvokePrivate(game, "Interact");

                            Assert(game.Room == RoomCatalog.Hub.Id && game.Projectiles.Count == 0,
                                "room transition clears projectiles before re-entry");
                            Assert(game.Enemies.All(enemy =>
                                    enemy.Alive &&
                                    enemy.Health == GameWorld.EnemyMaximumHealth &&
                                    enemy.BehaviorState == EnemyBehaviorState.Patrol &&
                                    enemy.AttackPhase == EnemyAttackPhase.None &&
                                    enemy.Position == RoomCatalog.Hub.EnemyDefinitions
                                        .Single(definition => definition.Id == enemy.Id).Spawn),
                                "re-entering a room restores its complete authored encounter");
            });
            yield return new TestCase("hostile projectiles damage once and respect player invulnerability", () =>
            {
                var game = new GameWorld();
                            var playerCenter = game.PlayerPosition + new Vector2(0, -GameWorld.PlayerBodyHeight / 2f);
                            var projectiles = GetField<List<ProjectileState>>(game, "projectiles");
                            projectiles.Add(new ProjectileState(
                                playerCenter, Vector2.Zero, 1,
                                ProjectileOwner.Enemy, ProjectileKind.BanditBullet, "first-bandit"));
                            projectiles.Add(new ProjectileState(
                                playerCenter, Vector2.Zero, 1,
                                ProjectileOwner.Enemy, ProjectileKind.BanditBullet, "second-bandit"));

                            game.Update(default, 0f);

                            Assert(game.Health == GameWorld.MaximumHealth - 1,
                                "simultaneous hostile hits consume only one health during invulnerability");
                            Assert(game.Projectiles.Count == 0,
                                "each hostile projectile is removed on its first player contact");
            });
            yield return new TestCase("completion freezes active encounters and hostile projectiles", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(280, 480));
                            game.Update(default, 0f);
                            game.Update(default, GameWorld.EnemyNoticeDuration);
                            game.Update(default, 0f);
                            game.Update(default, GameWorld.BanditTelegraphDuration);
                            Assert(game.Projectiles.Any(projectile => projectile.Owner == ProjectileOwner.Enemy),
                                "the completion fixture includes an active hostile projectile");

                            SetProperty(game, nameof(GameWorld.Completed), true);
                            var completed = CaptureWorld(game);
                            game.Update(new InputFrame(1, true, true, Vector2.UnitX, true, true, true, false), 5f);

                            Assert(CaptureWorld(game) == completed,
                                "completion freezes enemy AI, attack timers, hostile projectiles, damage, and player state");
            });
            yield return new TestCase("projectile damage affects the intended enemy without damaging its sibling", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(400, 480));

                            var initialHealth = game.Enemies.Select(enemy => enemy.Health).ToArray();
                            var aim = Vector2.Normalize(new Vector2(1, 0.25f));
                            game.Update(new InputFrame(0, false, false, aim, true, false, false, false), 0.14f);

                            Assert(game.Enemies[0].Health == initialHealth[0] - 1,
                                "projectile damage is applied to the enemy on its flight path");
                            Assert(game.Enemies[0].Alive, "a single projectile does not defeat the two-health enemy");
                            Assert(game.Enemies[1].Health == initialHealth[1],
                                "projectile damage does not affect the other enemy");
            });
            yield return new TestCase("projectile direction remains tied to the current aim direction", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));

                            var diagonalAim = Vector2.Normalize(new Vector2(1, -1));
                            game.Update(new InputFrame(0, false, false, diagonalAim, true, false, false, false), 0f);
                            Assert(game.Projectiles.Count == 1, "first aimed shot is spawned");
                            Assert(Vector2.Distance(game.Projectiles[0].Velocity, diagonalAim * 720f) < 0.01f,
                                "first projectile velocity follows diagonal aim");

                            game.Update(new InputFrame(0, false, false, -Vector2.UnitY, false, false, false, false), 0.2f);
                            game.Update(new InputFrame(0, false, false, -Vector2.UnitY, true, false, false, false), 0f);
                            Assert(game.Projectiles.Count == 2, "second aimed shot is spawned after the fire delay");
                            Assert(Vector2.Distance(game.Projectiles[1].Velocity, -Vector2.UnitY * 720f) < 0.01f,
                                "second projectile velocity follows the updated aim");
            });
            yield return new TestCase("horizontal movement updates facing used by horizontal shots", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));

                            game.Update(new InputFrame(1, false, false, Vector2.Zero, false, false, false, false), 0.1f);
                            game.Update(new InputFrame(-1, false, false, Vector2.Zero, false, false, false, false), 0.1f);
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(800, 480));
                            game.Update(new InputFrame(0, false, false, Vector2.Zero, true, false, false, false), 0f);

                            Assert(game.Projectiles.Count == 1, "left-facing horizontal shot is spawned");
                            Assert(Vector2.Distance(game.Projectiles[0].Velocity, -Vector2.UnitX * 720f) < 0.01f,
                                "walking left makes a subsequent horizontal shot travel left");

                            game.Update(new InputFrame(1, false, false, Vector2.Zero, false, false, false, false), 0.2f);
                            game.Update(new InputFrame(0, false, false, Vector2.Zero, true, false, false, false), 0f);

                            Assert(game.Projectiles.Any(projectile =>
                                    Vector2.Distance(projectile.Velocity, Vector2.UnitX * 720f) < 0.01f),
                                "walking right again makes a subsequent horizontal shot travel right");
            });
            yield return new TestCase("projectile spawn is above the player's feet at gun height", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));

                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false), 0f);

                            Assert(game.Projectiles.Count == 1, "horizontal aimed shot is spawned");
                            Assert(game.Projectiles[0].Position.Y < game.PlayerPosition.Y,
                                "horizontal projectile starts above the player's feet");
            });
            yield return new TestCase("paused updates preserve the complete deterministic world snapshot", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));
                            game.Update(new InputFrame(1, false, true, Vector2.UnitX, true, false, false, false), 0.05f);
                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, true, false, false), 0.05f);
                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, true), 0.01f);

                            Assert(game.IsPaused, "pause press edge enters the paused state");
                            var paused = CaptureWorld(game);

                            game.Update(new InputFrame(-1, true, true, -Vector2.UnitX, true, true, true, false), 5f);

                            Assert(CaptureWorld(game) == paused,
                                "paused updates leave positions, counters, timers, projectiles, enemies, and objective state unchanged");
            });
        }
    }
}
