using System.Collections;
using System.Reflection;

namespace Cowbania.Core.Tests.Enemies;

internal static class EnemiesTests
{
    private static readonly Type EnemyRuntimeType =
        typeof(GameWorld).Assembly.GetType("Cowbania.Core.Gameplay.Enemies.EnemyRuntime")!;
    private static readonly MethodInfo CanBeHitByPlayerProjectileMethod =
        typeof(GameWorld).Assembly.GetType("Cowbania.Core.Gameplay.Combat.ProjectileSystem")!
            .GetMethod("CanBeHitByPlayerProjectile", BindingFlags.Static | BindingFlags.NonPublic)!;

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
            yield return new TestCase("enemy definitions populate varied frontier encounters", () =>
            {
                var definitions = RoomCatalog.Hub.EnemyDefinitions.Concat(RoomCatalog.Branch.EnemyDefinitions).ToArray();
                            Assert(definitions.Length == 22,
                                "the expanded frontier contains twenty-two paced encounters");
                            Assert(definitions.Select(enemy => enemy.Id).Distinct(StringComparer.Ordinal).Count() == definitions.Length,
                                "all authored enemy ids are stable and unique");
                            foreach (var archetype in Enum.GetValues<EnemyArchetype>())
                                Assert(definitions.Count(enemy => enemy.Archetype == archetype) >= 3,
                                    $"{archetype} appears repeatedly across the expanded encounter mix");
                            Assert(definitions.Where(enemy => enemy.Archetype == EnemyArchetype.SidewinderSnake)
                                    .All(enemy => enemy.HorizontalLeash == 0),
                                "hidden sidewinders remain fixed to their authored ambush points");
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
                                    new Vector2(step % 2 == 0
                                        ? RoomCatalog.Hub.Bounds.X + GameWorld.PlayerBodyWidth / 2f
                                        : RoomCatalog.Hub.Bounds.Right - 140,
                                        RoomCatalog.Hub.Ground.Y));
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
            yield return new TestCase("dynamite armadillo notice roll and recovery timings are deterministic", () =>
            {
                var game = new GameWorld();
                            SetCurrentRoomEnemies(game, new EnemyDefinition(
                                "test-armadillo",
                                EnemyArchetype.DynamiteArmadillo,
                                new Vector2(520, 480),
                                120,
                                1));
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(640, 480));

                            game.Update(default, 0f);
                            Assert(game.Enemy.Archetype == EnemyArchetype.DynamiteArmadillo &&
                                   game.Enemy.BehaviorState == EnemyBehaviorState.Notice &&
                                   game.Enemy.AttackPhase == EnemyAttackPhase.None &&
                                   game.Enemy.FacingDirection == 1,
                                "the armadillo notices a same-lane player and freezes to face them");

                            game.Update(default, GameWorld.EnemyNoticeDuration);
                            Assert(game.Enemy.BehaviorState == EnemyBehaviorState.Attack &&
                                   game.Enemy.AttackPhase == EnemyAttackPhase.Active,
                                "the notice timer deterministically transitions into the roll");

                            var rollStartX = game.Enemy.Position.X;
                            game.Update(default, GameWorld.DynamiteArmadilloRollDuration / 2f);
                            Assert(game.Enemy.AttackPhase == EnemyAttackPhase.Active &&
                                   game.Enemy.Position.X > rollStartX,
                                "the active roll advances along the ground lane before its fixed duration completes");

                            game.Update(default, GameWorld.DynamiteArmadilloRollDuration / 2f + 0.01f);
                            Assert(game.Enemy.BehaviorState == EnemyBehaviorState.Attack &&
                                   game.Enemy.AttackPhase == EnemyAttackPhase.Recovery,
                                "the armadillo enters recovery immediately after the roll window ends");

                            game.Update(default, GameWorld.DynamiteArmadilloRecoveryDuration);
                            Assert(game.Enemy.BehaviorState == EnemyBehaviorState.Patrol &&
                                   game.Enemy.AttackPhase == EnemyAttackPhase.None,
                                "the deterministic recovery window returns the armadillo to patrol");
            });
            yield return new TestCase("dynamite armadillo roll respects leash bounds and enters recovery on impact", () =>
            {
                var game = new GameWorld();
                            SetCurrentRoomEnemies(game, new EnemyDefinition(
                                "test-armadillo",
                                EnemyArchetype.DynamiteArmadillo,
                                new Vector2(520, 480),
                                96,
                                1));
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(640, 480));

                            game.Update(default, 0f);
                            game.Update(default, GameWorld.EnemyNoticeDuration);
                            game.Update(default, 0.3f);

                            Assert(game.Enemy.AttackPhase == EnemyAttackPhase.Recovery,
                                "reaching the leash edge interrupts the roll and starts recovery immediately");
                            Assert(MathF.Abs(game.Enemy.Position.X - 616f) < 0.001f,
                                "the armadillo clamps exactly to its authored leash boundary");
                            Assert(game.Enemy.FacingDirection == -1,
                                "the blocked roll flips facing for the next patrol leg");
            });
            yield return new TestCase("dynamite armadillo consumes blocked bullets and only harms during roll", () =>
            {
                var game = new GameWorld();
                            SetCurrentRoomEnemies(game, new EnemyDefinition(
                                "test-armadillo",
                                EnemyArchetype.DynamiteArmadillo,
                                new Vector2(520, 480),
                                120,
                                1));

                            var projectiles = GetField<List<ProjectileState>>(game, "projectiles");
                            var immuneHit = new ProjectileState(new Vector2(520, 456), Vector2.Zero, 1);
                            projectiles.Add(immuneHit);

                            var startingHealth = game.Enemy.Health;
                            game.Update(default, 0f);
                            Assert(game.Projectiles.Count == 0,
                                "the armadillo shell consumes the player bullet on impact");
                            Assert(game.Enemy.Health == startingHealth && game.Enemy.Alive,
                                "armadillo overlaps do not deal projectile damage");
                            Assert(game.ArmadilloShotBlockedThisUpdate,
                                "the blocked impact exposes one-frame presentation feedback");

                            game.Update(default, 0f);
                            Assert(!game.ArmadilloShotBlockedThisUpdate,
                                "the blocked-impact feedback resets on the next update");

                            SetProperty(game, nameof(GameWorld.PlayerPosition), game.Enemy.Position);
                            SetProperty(game, nameof(GameWorld.Health), GameWorld.MaximumHealth);
                            SetField(game, "invulnerabilityTimer", 0f);

                            game.Update(default, 0f);
                            Assert(game.Health == GameWorld.MaximumHealth,
                                "patrol contact is harmless in the v1 hazard pass");

                            game.Update(default, GameWorld.EnemyNoticeDuration);
                            Assert(game.Health == GameWorld.MaximumHealth,
                                "notice contact is also harmless before the roll starts");

                            game.Update(default, 0f);
                            Assert(game.Health == GameWorld.MaximumHealth - 1,
                                "roll contact consumes exactly one health point");

                            SetProperty(game, nameof(GameWorld.Enemy), game.Enemy with
                            {
                                BehaviorState = EnemyBehaviorState.Attack,
                                AttackPhase = EnemyAttackPhase.Recovery,
                                Position = game.PlayerPosition
                            });
                            SetField(game, "invulnerabilityTimer", 0f);
                            game.Update(default, 0f);
                            Assert(game.Health == GameWorld.MaximumHealth - 1,
                                "recovery contact does not damage the player");
            });
            yield return new TestCase("sidewinder snake trigger timing and re-arm require a fresh radius entry", () =>
            {
                var game = new GameWorld();
                            SetCurrentRoomEnemies(game, new EnemyDefinition(
                                "test-snake",
                                EnemyArchetype.SidewinderSnake,
                                new Vector2(520, 480),
                                0,
                                1));
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(620, 480));

                            Assert(game.Enemy.BehaviorState == EnemyBehaviorState.Hidden,
                                "sidewinders start hidden and inert");

                            game.Update(default, 0f);
                            Assert(game.Enemy.BehaviorState == EnemyBehaviorState.Attack &&
                                   game.Enemy.AttackPhase == EnemyAttackPhase.Telegraph,
                                "entering the trigger radius raises the snake immediately");

                            game.Update(default, GameWorld.SidewinderSnakeRisingDuration);
                            Assert(game.Enemy.AttackPhase == EnemyAttackPhase.Active,
                                "the rising window deterministically transitions into the exposed window");

                            game.Update(default, GameWorld.SidewinderSnakeExposedDuration);
                            Assert(game.Enemy.AttackPhase == EnemyAttackPhase.Recovery,
                                "the exposed window deterministically transitions into retreating");

                            game.Update(default, GameWorld.SidewinderSnakeRetreatDuration);
                            Assert(game.Enemy.BehaviorState == EnemyBehaviorState.Hidden,
                                "retreating completes back into the hidden burrow state");

                            game.Update(default, 0f);
                            Assert(game.Enemy.BehaviorState == EnemyBehaviorState.Hidden,
                                "remaining inside the trigger radius does not immediately re-arm the snake");

                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(700, 480));
                            game.Update(default, 0f);
                            Assert(game.Enemy.BehaviorState == EnemyBehaviorState.Hidden,
                                "fully leaving the trigger radius preserves the hidden state");

                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(620, 480));
                            game.Update(default, 0f);
                            Assert(game.Enemy.BehaviorState == EnemyBehaviorState.Attack &&
                                   game.Enemy.AttackPhase == EnemyAttackPhase.Telegraph,
                                "a fresh trigger-radius entry re-arms and raises the snake again");
            });
            yield return new TestCase("sidewinder snake is defeated by a single shot while rising", () =>
            {
                var game = new GameWorld();
                            SetCurrentRoomEnemies(game, new EnemyDefinition(
                                "test-snake",
                                EnemyArchetype.SidewinderSnake,
                                new Vector2(520, 480),
                                0,
                                1));
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(620, 480));

                            game.Update(default, 0f);
                            GetField<List<ProjectileState>>(game, "projectiles").Add(
                                new ProjectileState(new Vector2(520, 456), Vector2.Zero, 1));

                            game.Update(default, 0f);
                            Assert(!game.Enemy.Alive &&
                                   game.Enemy.BehaviorState == EnemyBehaviorState.Defeated &&
                                   game.Enemy.Health == 0,
                                "a rising snake has one health and is immediately defeated by any bullet");
                            Assert(game.Projectiles.Count == 0,
                                "the bullet is consumed by the first valid snake hit");
            });
            yield return new TestCase("sidewinder snake is defeated by a single shot while exposed", () =>
            {
                var game = new GameWorld();
                            SetCurrentRoomEnemies(game, new EnemyDefinition(
                                "test-snake",
                                EnemyArchetype.SidewinderSnake,
                                new Vector2(520, 480),
                                0,
                                1));
                            SetProperty(game, nameof(GameWorld.Enemy), game.Enemy with
                            {
                                BehaviorState = EnemyBehaviorState.Attack,
                                AttackPhase = EnemyAttackPhase.Active,
                                Health = GameWorld.SidewinderSnakeHealth,
                                Alive = true
                            });
                            GetField<List<ProjectileState>>(game, "projectiles").Add(
                                new ProjectileState(new Vector2(520, 456), Vector2.Zero, 1));

                            game.Update(default, 0f);
                            Assert(!game.Enemy.Alive &&
                                   game.Enemy.BehaviorState == EnemyBehaviorState.Defeated &&
                                   game.Enemy.Health == 0,
                                "an exposed snake also dies to a single player bullet");
                            Assert(game.Projectiles.Count == 0,
                                "the exposed-hit bullet is consumed on impact");
            });
            yield return new TestCase("sidewinder snake projectile hit gating excludes hidden and retreating phases", () =>
            {
                var runtime = CreateEnemyRuntime(new EnemyDefinition(
                    "test-snake",
                    EnemyArchetype.SidewinderSnake,
                    new Vector2(520, 480),
                    0,
                    1));

                            Assert(!CanBeHitByPlayerProjectile(runtime),
                                "hidden snakes are excluded from projectile hit gating before any overlap checks");

                            SetEnemyRuntimeState(runtime, EnemyBehaviorState.Attack, EnemyAttackPhase.Telegraph);
                            Assert(CanBeHitByPlayerProjectile(runtime),
                                "rising snakes become targetable for the one-shot kill window");

                            SetEnemyRuntimeState(runtime, EnemyBehaviorState.Attack, EnemyAttackPhase.Active);
                            Assert(CanBeHitByPlayerProjectile(runtime),
                                "fully exposed snakes remain targetable until the retreat begins");

                            SetEnemyRuntimeState(runtime, EnemyBehaviorState.Attack, EnemyAttackPhase.Recovery);
                            Assert(!CanBeHitByPlayerProjectile(runtime),
                                "retreating snakes drop out of projectile hit gating as soon as exposure ends");
            });
            yield return new TestCase("sidewinder snake stays untargetable while hidden and only deals contact damage while exposed", () =>
            {
                var game = new GameWorld();
                            SetCurrentRoomEnemies(game, new EnemyDefinition(
                                "test-snake",
                                EnemyArchetype.SidewinderSnake,
                                new Vector2(520, 480),
                                0,
                                1));

                            var projectiles = GetField<List<ProjectileState>>(game, "projectiles");
                            var hiddenShot = new ProjectileState(new Vector2(520, 456), Vector2.Zero, 1);
                            projectiles.Add(hiddenShot);

                            game.Update(default, 0f);
                            Assert(game.Projectiles.Count == 1 && game.Projectiles[0] == hiddenShot,
                                "hidden snakes do not consume or intercept bullets");
                            Assert(game.Enemy.Health == GameWorld.SidewinderSnakeHealth &&
                                   game.Enemy.BehaviorState == EnemyBehaviorState.Hidden,
                                "hidden snakes remain untargetable and keep their full health");
                            projectiles.Clear();

                            SetProperty(game, nameof(GameWorld.PlayerPosition), game.Enemy.Position);
                            SetProperty(game, nameof(GameWorld.Health), GameWorld.MaximumHealth);

                            SetProperty(game, nameof(GameWorld.Enemy), game.Enemy with
                            {
                                BehaviorState = EnemyBehaviorState.Attack,
                                AttackPhase = EnemyAttackPhase.Telegraph,
                                Position = game.PlayerPosition,
                                Health = GameWorld.SidewinderSnakeHealth,
                                Alive = true
                            });
                            SetField(game, "invulnerabilityTimer", 0f);
                            game.Update(default, 0f);
                            Assert(game.Health == GameWorld.MaximumHealth,
                                "rising contact is harmless until the snake is fully exposed");

                            SetProperty(game, nameof(GameWorld.Enemy), game.Enemy with
                            {
                                BehaviorState = EnemyBehaviorState.Attack,
                                AttackPhase = EnemyAttackPhase.Active,
                                Position = game.PlayerPosition,
                                Health = GameWorld.SidewinderSnakeHealth,
                                Alive = true
                            });
                            SetField(game, "invulnerabilityTimer", 0f);
                            game.Update(default, 0f);
                            Assert(game.Health == GameWorld.MaximumHealth - 1,
                                "exposed contact consumes exactly one health point");

                            SetProperty(game, nameof(GameWorld.Enemy), game.Enemy with
                            {
                                BehaviorState = EnemyBehaviorState.Attack,
                                AttackPhase = EnemyAttackPhase.Recovery,
                                Position = game.PlayerPosition,
                                Health = GameWorld.SidewinderSnakeHealth,
                                Alive = true
                            });
                            SetField(game, "invulnerabilityTimer", 0f);
                            game.Update(default, 0f);
                            Assert(game.Health == GameWorld.MaximumHealth - 1,
                                "retreating contact is harmless again once the exposed window ends");
            });
        }
    }

    private static void SetCurrentRoomEnemies(GameWorld game, params EnemyDefinition[] definitions)
    {
        _ = game.Enemies;

        var stateField = typeof(GameWorld).GetField(
            "state",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var state = stateField.GetValue(game)!;
        var enemiesByRoomField = state.GetType().GetField(
            "EnemiesByRoom",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        var byRoom = (IDictionary)enemiesByRoomField.GetValue(state)!;
        var enemies = (IList)byRoom[game.Room]!;
        enemies.Clear();
        foreach (var definition in definitions)
            enemies.Add(CreateEnemyRuntime(definition));
    }

    private static object CreateEnemyRuntime(EnemyDefinition definition) =>
        EnemyRuntimeType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Single(ctor => ctor.GetParameters() is [{ ParameterType: var type }] && type == typeof(EnemyDefinition))
            .Invoke([definition]);

    private static bool CanBeHitByPlayerProjectile(object enemyRuntime) =>
        (bool)CanBeHitByPlayerProjectileMethod.Invoke(null, [enemyRuntime])!;

    private static void SetEnemyRuntimeState(
        object enemyRuntime,
        EnemyBehaviorState behaviorState,
        EnemyAttackPhase attackPhase)
    {
        SetEnemyRuntimeProperty(enemyRuntime, "BehaviorState", behaviorState);
        SetEnemyRuntimeProperty(enemyRuntime, "AttackPhase", attackPhase);
        SetEnemyRuntimeProperty(enemyRuntime, "Alive", true);
    }

    private static void SetEnemyRuntimeProperty<T>(object enemyRuntime, string propertyName, T value)
    {
        var property = EnemyRuntimeType.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        property.SetValue(enemyRuntime, value);
    }
}
