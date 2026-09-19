using System.Numerics;
using System.Reflection;
using Cowbania.Core;

static class Tests
{
    static void Main()
    {
        Run("animation clocks loop and complete one-shot clips deterministically", () =>
        {
            var loop = new AnimationClip("loop", new[] { new AnimationFrame("a"), new AnimationFrame("b") }, 2);
            var clock = new AnimationClock();
            clock.Advance(0.5f, loop);
            Assert(clock.CurrentFrame(loop).AssetKey == "b", "loop advances at the frame boundary");
            clock.Advance(0.5f, loop);
            Assert(clock.CurrentFrame(loop).AssetKey == "a" && !clock.IsComplete, "loop wraps without completing");

            var oneShot = loop with { PlaybackMode = AnimationPlaybackMode.OneShot };
            clock.Reset();
            clock.Advance(0.5f, oneShot);
            Assert(clock.CurrentFrame(oneShot).AssetKey == "b" && !clock.IsComplete,
                "one-shot remains active while showing its final frame");
            clock.Advance(0.5f, oneShot);
            Assert(clock.IsComplete && clock.CurrentFrame(oneShot).AssetKey == "b",
                "one-shot completes after the final frame duration");
        });

        Run("presentation selector prioritizes transient and locomotion states", () =>
        {
            Assert(PresentationStateSelector.SelectPlayer(new PlayerPresentationInput(false, true, false, false, false, false, false)) == PresentationAnimationState.Idle, "idle selection");
            Assert(PresentationStateSelector.SelectPlayer(new PlayerPresentationInput(true, true, false, false, false, false, false)) == PresentationAnimationState.Run, "run selection");
            Assert(PresentationStateSelector.SelectPlayer(new PlayerPresentationInput(false, false, true, false, false, false, false)) == PresentationAnimationState.Jump, "jump selection");
            Assert(PresentationStateSelector.SelectPlayer(new PlayerPresentationInput(false, false, false, false, false, false, false)) == PresentationAnimationState.Fall, "fall selection");
            Assert(PresentationStateSelector.SelectPlayer(new PlayerPresentationInput(true, true, false, true, true, true, true)) == PresentationAnimationState.Hurt, "hurt has priority");
            Assert(PresentationStateSelector.SelectEnemy(true) == PresentationAnimationState.EnemyIdle, "enemy selection");
            Assert(PresentationStateSelector.SelectPickup() == PresentationAnimationState.PickupFloat, "pickup selection");
        });

        Run("enemy presentation selects readable deterministic telegraphs", () =>
        {
            var banditTelegraph = PresentationStateSelector.SelectEnemy(new EnemyState(
                "bandit", EnemyArchetype.Bandit, EnemyBehaviorState.Attack, EnemyAttackPhase.Telegraph,
                Vector2.Zero, Vector2.Zero, -1, 2, true, 0.5f, 0.25f));
            var wildlifeActive = PresentationStateSelector.SelectEnemy(new EnemyState(
                "wildlife", EnemyArchetype.Wildlife, EnemyBehaviorState.Attack, EnemyAttackPhase.Active,
                Vector2.Zero, Vector2.UnitX, 1, 2, true, 0.5f, 0.25f));
            var defeated = PresentationStateSelector.SelectEnemy(new EnemyState(
                "defeated", EnemyArchetype.Bandit, EnemyBehaviorState.Defeated, EnemyAttackPhase.None,
                Vector2.Zero, Vector2.Zero, 0, 0, false, 1f, 0f));

            Assert(banditTelegraph.AnimationState == PresentationAnimationState.BanditAttack &&
                   banditTelegraph.TelegraphMarker == EnemyTelegraphMarker.BanditAimLine &&
                   banditTelegraph.FacingDirection == -1 &&
                   !banditTelegraph.AttackActive,
                "bandit telegraph selects its ranged aim cue without becoming active");
            Assert(wildlifeActive.AnimationState == PresentationAnimationState.WildlifeLunge &&
                   wildlifeActive.TelegraphMarker == EnemyTelegraphMarker.WildlifeLungeTrail &&
                   wildlifeActive.AttackActive,
                "wildlife active attack selects its lunge trail and active flag");
            Assert(defeated.AnimationState == PresentationAnimationState.EnemyDefeated &&
                   defeated.TelegraphMarker == EnemyTelegraphMarker.None &&
                   defeated.FacingDirection == 1,
                "defeated enemies suppress telegraphs and normalize facing");
            Assert(banditTelegraph.PaletteTint != wildlifeActive.PaletteTint,
                "bandit and wildlife use stable distinct palette tints");
        });

        Run("enemy presentation clocks reset when the selected state changes", () =>
        {
            var clock = new PresentationAnimationClock();
            clock.Advance(0.25f, PresentationAnimationState.BanditPatrol);
            Assert(clock.CurrentFrameIndex == 1, "bandit patrol advances deterministically");

            clock.Advance(0f, PresentationAnimationState.BanditNotice);
            Assert(clock.CurrentState == PresentationAnimationState.BanditNotice &&
                   clock.CurrentFrameIndex == 0 &&
                   clock.CurrentFrame().AssetKey == "Frontier/Bandit/notice_0.png",
                "changing enemy state resets the clock to the authored notice pose");
        });

        Run("player animation selector covers idle run jump fall shoot reload hurt and dash", () =>
        {
            Assert(PlayerAnimationStateSelector.Select(Vector2.Zero, true) == PlayerAnimationState.Idle,
                "stationary grounded player selects idle");
            Assert(PlayerAnimationStateSelector.Select(new Vector2(1, 0), true) == PlayerAnimationState.Run,
                "moving grounded player selects run");
            Assert(PlayerAnimationStateSelector.Select(new Vector2(0, -1), false) == PlayerAnimationState.Jump,
                "rising airborne player selects jump");
            Assert(PlayerAnimationStateSelector.Select(new Vector2(0, 1), false) == PlayerAnimationState.Fall,
                "descending airborne player selects fall");
            Assert(PlayerAnimationStateSelector.Select(Vector2.Zero, true, shooting: true) == PlayerAnimationState.Shoot,
                "shooting player selects shoot");
            Assert(PlayerAnimationStateSelector.Select(Vector2.Zero, true, reloading: true) == PlayerAnimationState.Reload,
                "reloading player selects reload");
            Assert(PlayerAnimationStateSelector.Select(Vector2.Zero, true, hurt: true) == PlayerAnimationState.Hurt,
                "hurt player selects hurt");
            Assert(PlayerAnimationStateSelector.Select(Vector2.Zero, true, dashing: true) == PlayerAnimationState.Dash,
                "dashing player selects dash");
        });

        Run("presentation snapshots preserve feet facing and muzzle anchors", () =>
        {
            var feet = new Vector2(80, 480);
            var muzzle = feet + GameWorld.PlayerMuzzleOffset + Vector2.UnitX * GameWorld.PlayerMuzzleDistance;
            var snapshot = new ActorPresentationState(PlayerAnimationState.Shoot, feet, muzzle, -1);

            Assert(snapshot.AnimationState == PlayerAnimationState.Shoot, "snapshot preserves selected animation state");
            Assert(snapshot.FeetAnchor == feet, "snapshot preserves the feet anchor");
            Assert(snapshot.MuzzleAnchor == muzzle, "snapshot preserves the muzzle anchor");
            Assert(snapshot.FacingDirection == -1, "snapshot preserves horizontal facing");
        });

        Run("fire and reload honor the fixed six-round cylinder", () =>
        {
            var game = new GameWorld();
            // Isolate cylinder accounting from projectile collision/removal.
            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));

            for (var i = 0; i < 6; i++)
            {
                game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false), 0.2f);
            }

            Assert(game.Ammo == 0, "six accepted shots consume the full cylinder");
            Assert(game.Projectiles.Count == 6, "each accepted shot spawns one projectile");
            Assert(game.Projectiles.All(projectile => projectile.Position.Y == 456), "projectiles spawn from the elevated gun muzzle");

            game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, true, false, false), 0.1f);
            game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, false), 1.2f);

            Assert(game.Ammo == 6, "reload restores the cylinder to six rounds");
            Assert(game.IsReloading == false, "reload completes without leaving gun reloading active");
        });

        Run("projectiles spawn at the player's gun muzzle", () =>
        {
            var game = new GameWorld();
            game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false), 0);

            Assert(game.Projectiles.Count == 1, "firing creates one projectile");
            Assert(game.Projectiles[0].Position ==
                game.PlayerPosition + GameWorld.PlayerMuzzleOffset + Vector2.UnitX * GameWorld.PlayerMuzzleDistance,
                "projectile starts at the gun muzzle");
        });

        Run("horizontal movement remains grounded and advances the player deterministically", () =>
        {
            var game = new GameWorld();

            for (var i = 0; i < 12; i++)
            {
                game.Update(new InputFrame(1, false, false, Vector2.UnitX, false, false, false, false), 0.25f);
            }

            Assert(game.PlayerPosition.X > 80, "horizontal input advances the player");
            Assert(game.PlayerPosition.Y == 480, "grounded movement stays on the shared ground plane");
        });

        Run("jump uses screen-space signs and lands on a raised platform", () =>
        {
            var game = new GameWorld();
            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(350, 480));
            game.Update(new InputFrame(0, true, false, Vector2.UnitX, false, false, false, false), 0.05f);
            Assert(game.PlayerVelocity.Y < 0, "jump velocity points upward as negative Y");
            Assert(game.PlayerPosition.Y < 480, "jump lifts the feet above the ground");

            for (var i = 0; i < 30; i++)
                game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, false), 0.05f);

            Assert(game.PlayerPosition.Y == 380, "falling player lands on the shared raised platform");
            Assert(game.PlayerVelocity.Y == 0, "landing clears downward velocity");
        });

        Run("jump moves upward immediately in screen space", () =>
        {
            var game = new GameWorld();
            var startingY = game.PlayerPosition.Y;
            game.Update(new InputFrame(0, true, false, Vector2.UnitX, false, false, false, false), 0.016f);
            Assert(game.PlayerPosition.Y < startingY, "jump immediately decreases screen-space Y");
            Assert(game.PlayerVelocity.Y < 0, "jump starts with upward velocity");
            Assert(game.LastJumpRequestOutcome == JumpRequestOutcome.Accepted,
                "grounded jump records an accepted request outcome");
        });

        Run("jump diagnostics distinguish rejected requests", () =>
        {
            var airborne = new GameWorld();
            SetProperty(airborne, nameof(GameWorld.PlayerPosition), new Vector2(500, 400));
            airborne.Update(new InputFrame(0, true, false, Vector2.UnitX, false, false, false, false), 0.016f);
            Assert(airborne.LastJumpRequestOutcome == JumpRequestOutcome.RejectedNotGrounded,
                "unsupported jump records a not-grounded rejection");

            var paused = new GameWorld();
            paused.Update(new InputFrame(0, true, false, Vector2.UnitX, false, false, false, true), 0.016f);
            Assert(paused.LastJumpRequestOutcome == JumpRequestOutcome.RejectedPaused,
                "jump pressed while pausing records a paused rejection");
        });

        Run("fatal exception formatting preserves source, termination, and full details", () =>
        {
            var exception = new AggregateException(
                "jump audio failed",
                new InvalidOperationException("decoder unavailable", new ArgumentException("device unavailable")),
                new InvalidOperationException("playback unavailable"));
            var details = FatalExceptionFormatter.Format("Game.Run", exception, true);
            Assert(details.Contains("source=\"Game.Run\""), "fatal details include source");
            Assert(details.Contains("terminating=True"), "fatal details include termination state");
            Assert(details.Contains("System.AggregateException: jump audio failed"),
                "fatal details include full primary exception output");
            Assert(details.Contains("System.ArgumentException: device unavailable"),
                "fatal details include inner exception details");
            Assert(details.Contains("aggregateInner[0].ToString()") &&
                   details.Contains("aggregateInner[1].ToString()"),
                "fatal details enumerate every aggregate inner exception");
        });

        Run("gravity returns the player to a valid support surface", () =>
        {
            var game = new GameWorld();
            game.Update(new InputFrame(0, true, false, Vector2.UnitX, false, false, false, false), 0.016f);
            for (var i = 0; i < 120; i++)
                game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, false), 0.016f);
            Assert(game.PlayerPosition.Y == RoomCatalog.Hub.Ground.Y, "gravity returns the player to the hub ground");
            Assert(game.PlayerVelocity.Y == 0, "landing clears vertical velocity");
        });

        Run("grounded state is derived from collision geometry", () =>
        {
            var game = new GameWorld();
            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(500, 380));
            SetProperty(game, nameof(GameWorld.PlayerVelocity), Vector2.Zero);
            game.Update(new InputFrame(0, true, false, Vector2.UnitX, false, false, false, false), 0.016f);
            Assert(game.PlayerPosition.Y > 380, "jump input is ignored when the player is not supported");
            Assert(game.PlayerVelocity.Y > 0, "unsupported player continues falling under gravity");
        });

        Run("player lands on a raised platform", () =>
        {
            var game = new GameWorld();
            var platform = RoomCatalog.Hub.Solids[1];
            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(platform.X + 40, platform.Y));
            SetProperty(game, nameof(GameWorld.PlayerVelocity), Vector2.Zero);
            game.Update(new InputFrame(0, true, false, Vector2.UnitX, false, false, false, false), 0.016f);
            for (var i = 0; i < 120; i++)
                game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, false), 0.016f);
            Assert(game.PlayerPosition.Y == platform.Y, "descending player lands on the raised platform");
            Assert(game.PlayerVelocity.Y == 0, "raised-platform landing clears vertical velocity");
        });

        Run("hub and branch expose distinct stage geometry", () =>
        {
            Assert(RoomCatalog.Hub.Id != RoomCatalog.Branch.Id, "hub and branch have distinct room ids");
            Assert(RoomCatalog.Hub.Name != RoomCatalog.Branch.Name, "hub and branch have distinct names");
            Assert(!RoomCatalog.Hub.Solids.SequenceEqual(RoomCatalog.Branch.Solids), "hub and branch solids are distinct");
            Assert(RoomCatalog.Hub.Solids[1] != RoomCatalog.Branch.Solids[1], "raised-platform geometry differs between rooms");
        });

        Run("room visual selection remains keyed by RoomCatalog ids", () =>
        {
            Assert(RoomCatalog.ForId(RoomCatalog.Hub.Id) == RoomCatalog.Hub,
                "hub id resolves to the hub definition");
            Assert(RoomCatalog.ForId(RoomCatalog.Branch.Id) == RoomCatalog.Branch,
                "branch id resolves to the branch definition");
            Assert(RoomCatalog.ForId(0).Id == RoomCatalog.Hub.Id,
                "room zero remains the hub visual/state key");
            Assert(RoomCatalog.ForId(1).Id == RoomCatalog.Branch.Id,
                "room one remains the branch visual/state key");
            Assert(RoomCatalog.ForId(999) == RoomCatalog.Hub,
                "unknown room ids retain the catalog fallback");

            var game = new GameWorld();
            Assert(game.CurrentRoom == RoomCatalog.Hub, "world starts with hub room state");
            SetProperty(game, nameof(GameWorld.Room), RoomCatalog.Branch.Id);
            Assert(game.CurrentRoom == RoomCatalog.Branch, "world room state selects branch by catalog id");
        });

        Run("catalog surfaces provide the renderer's collision-aligned stage contract", () =>
        {
            foreach (var room in new[] { RoomCatalog.Hub, RoomCatalog.Branch })
            {
                Assert(room.Ground == room.Solids[0], $"{room.Name} ground is the first authored surface");
                Assert(room.Solids.Length >= 2, $"{room.Name} exposes visible ground and raised surfaces");

                foreach (var surface in room.Solids)
                {
                    Assert(surface.Width > 0 && surface.Height > 0,
                        $"{room.Name} surfaces have drawable dimensions");
                    Assert(surface.X >= room.Bounds.X && surface.Right <= room.Bounds.Right,
                        $"{room.Name} surface horizontal bounds stay inside the room");
                    Assert(surface.Y >= room.Bounds.Y && surface.Bottom <= room.Bounds.Bottom,
                        $"{room.Name} surface vertical bounds stay inside the room");
                    Assert(surface.Right == surface.X + surface.Width &&
                           surface.Bottom == surface.Y + surface.Height,
                        $"{room.Name} surface draw bounds match collision rectangle edges");
                }
            }
        });

        Run("stage presentation keeps RoomCatalog metadata unchanged", () =>
        {
            var expected = new[]
            {
                (RoomCatalog.Hub, 0, "Hub", new RoomRect(0, 0, 1600, 576), new Vector2(80, 480), new Vector2(80, 480), new Vector2(1440, 480)),
                (RoomCatalog.Branch, 1, "Branch", new RoomRect(0, 0, 1600, 576), new Vector2(40, 480), new Vector2(760, 480), new Vector2(1440, 480))
            };

            foreach (var (room, id, name, bounds, spawn, checkpoint, shortcut) in expected)
            {
                Assert(room.Id == id && room.Name == name, $"{name} identity remains authored");
                Assert(room.Bounds == bounds, $"{name} bounds remain authored");
                Assert(room.Spawn == spawn && room.Checkpoint == checkpoint && room.Shortcut == shortcut,
                    $"{name} interaction anchors remain authored");
            }

            Assert(RoomCatalog.Hub.EnemySpawns.SequenceEqual(new[] { new Vector2(520, 480), new Vector2(1040, 480) }),
                "hub enemy placements remain authored");
            Assert(RoomCatalog.Branch.EnemySpawns.SequenceEqual(new[] { new Vector2(520, 480), new Vector2(1120, 480) }),
                "branch enemy placements remain authored");
            Assert(RoomCatalog.Hub.PickupDefinitions.SequenceEqual(new[]
                {
                    new PickupDefinition("hub-currency", new Vector2(1120, 448), PickupType.Currency),
                    new PickupDefinition("hub-health", new Vector2(1280, 448), PickupType.Health)
                }),
                "hub pickup placements remain authored");
            Assert(RoomCatalog.Branch.PickupDefinitions.SequenceEqual(new[]
                {
                    new PickupDefinition("branch-reserve-ammo", new Vector2(760, 448), PickupType.ReserveAmmo),
                    new PickupDefinition("branch-currency", new Vector2(1440, 448), PickupType.Currency)
                }),
                "branch pickup placements remain authored");
        });

        Run("presentation-facing updates do not mutate collision authority", () =>
        {
            var before = new[] { RoomCatalog.Hub, RoomCatalog.Branch }
                .Select(room => (Id: room.Id, Name: room.Name, Bounds: room.Bounds, Solids: room.Solids.ToArray(),
                    Spawn: room.Spawn, Checkpoint: room.Checkpoint, Shortcut: room.Shortcut,
                    EnemySpawns: room.EnemySpawns.ToArray(), Pickups: room.Pickups.ToArray()))
                .ToArray();
            var game = new GameWorld();

            for (var i = 0; i < 60; i++)
                game.Update(new InputFrame(i % 2 == 0 ? 1 : -1, i == 0, i == 20, Vector2.UnitX,
                    i % 7 == 0, false, false, false), 0.016f);

            var after = new[] { RoomCatalog.Hub, RoomCatalog.Branch }
                .Select(room => (Id: room.Id, Name: room.Name, Bounds: room.Bounds, Solids: room.Solids.ToArray(),
                    Spawn: room.Spawn, Checkpoint: room.Checkpoint, Shortcut: room.Shortcut,
                    EnemySpawns: room.EnemySpawns.ToArray(), Pickups: room.Pickups.ToArray()))
                .ToArray();
            Assert(before.Length == after.Length &&
                    before.Zip(after).All(pair =>
                        pair.First.Id == pair.Second.Id &&
                        pair.First.Name == pair.Second.Name &&
                        pair.First.Bounds == pair.Second.Bounds &&
                        pair.First.Solids.SequenceEqual(pair.Second.Solids) &&
                        pair.First.Spawn == pair.Second.Spawn &&
                        pair.First.Checkpoint == pair.Second.Checkpoint &&
                        pair.First.Shortcut == pair.Second.Shortcut &&
                        pair.First.EnemySpawns.SequenceEqual(pair.Second.EnemySpawns) &&
                        pair.First.Pickups.SequenceEqual(pair.Second.Pickups)),
                "room metadata remains unchanged after simulation updates");
            Assert(game.CurrentRoom == RoomCatalog.ForId(game.Room), "world collision room still resolves through RoomCatalog");
        });

        Run("equivalent stage runs produce deterministic gameplay state", () =>
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

        Run("room definitions expose multiple enemy placements", () =>
        {
            foreach (var room in new[] { RoomCatalog.Hub, RoomCatalog.Branch })
            {
                Assert(room.EnemySpawns.Length >= 2, $"{room.Name} defines multiple enemy placements");
                Assert(room.EnemySpawns.Distinct().Count() == room.EnemySpawns.Length,
                    $"{room.Name} enemy placements are distinct");
            }
        });

        Run("game world creates enemies at the current room placements", () =>
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

        Run("enemy definitions assign stable bandit and wildlife encounters", () =>
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

        Run("enemy snapshots expose deterministic notice and chase state", () =>
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

        Run("bandit attack telegraphs then emits one hostile projectile", () =>
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

        Run("wildlife lunge damage occurs once during active attack", () =>
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

        Run("room re-entry resets the encounter and clears every projectile", () =>
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

            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(1570, 480));
            InvokePrivate(game, "Interact");
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

        Run("release five attack timelines remain deterministic", () =>
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

        Run("hostile projectiles damage once and respect player invulnerability", () =>
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

        Run("enemy movement remains inside authored room and leash bounds", () =>
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

        Run("defeated enemies remain inert", () =>
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

        Run("lethal hostile damage restores the authored encounter", () =>
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

        Run("completion freezes active encounters and hostile projectiles", () =>
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

        Run("projectile damage affects the intended enemy without damaging its sibling", () =>
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

        Run("spawn checkpoint and shortcut positions are supported by room geometry", () =>
        {
            foreach (var room in new[] { RoomCatalog.Hub, RoomCatalog.Branch })
            {
                Assert(IsSupported(room, room.Spawn), $"{room.Name} spawn is supported");
                Assert(IsSupported(room, room.Checkpoint), $"{room.Name} checkpoint is supported");
                Assert(IsSupported(room, room.Shortcut), $"{room.Name} shortcut is supported");
            }
        });

        Run("player feet remain exactly on the supporting surface when grounded", () =>
        {
            var game = new GameWorld();

            Assert(game.PlayerPosition.Y == RoomCatalog.Hub.Ground.Y,
                "initial player feet equal the hub ground top");

            for (var i = 0; i < 120; i++)
                game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, false), 0.016f);

            Assert(game.PlayerPosition.Y == RoomCatalog.Hub.Ground.Y,
                "settled player feet equal the hub ground top");

            var platform = RoomCatalog.Hub.Solids[1];
            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(platform.X + 40, platform.Y));
            SetProperty(game, nameof(GameWorld.PlayerVelocity), Vector2.Zero);
            game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, false), 0f);

            Assert(game.PlayerPosition.Y == platform.Y,
                "grounded player feet equal the raised platform top");
        });

        Run("projectile direction remains tied to the current aim direction", () =>
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

        Run("horizontal movement updates facing used by horizontal shots", () =>
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

        Run("horizontal movement updates facing and neutral fire direction", () =>
        {
            var game = new GameWorld();
            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));

            game.Update(new InputFrame(1, false, false, Vector2.Zero, false, false, false, false), 0f);
            Assert(game.FacingDirection == 1, "rightward movement faces right");
            game.Update(new InputFrame(-1, false, false, Vector2.Zero, true, false, false, false), 0f);

            Assert(game.FacingDirection == -1, "leftward movement faces left");
            Assert(game.AimDirection == -Vector2.UnitX, "neutral aim follows the current facing");
            Assert(game.Projectiles[0].Velocity == -Vector2.UnitX * 720f,
                "neutral fire travels in the current facing direction");
        });

        Run("explicit vertical and diagonal aim override facing without losing horizontal facing", () =>
        {
            var game = new GameWorld();
            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));

            game.Update(new InputFrame(-1, false, false, Vector2.Zero, false, false, false, false), 0f);
            game.Update(new InputFrame(0, false, false, -Vector2.UnitY, true, false, false, false), 0f);
            Assert(game.Projectiles[0].Velocity == -Vector2.UnitY * 720f,
                "explicit vertical aim fires vertically");
            Assert(game.FacingDirection == -1, "vertical aim preserves horizontal facing");

            game.Update(new InputFrame(0, false, false, Vector2.Normalize(new Vector2(1, -1)), false, false, false, false), 0.2f);
            Assert(game.FacingDirection == 1, "explicit diagonal aim updates horizontal facing");
            Assert(Vector2.Distance(game.AimDirection, Vector2.Normalize(new Vector2(1, -1))) < 0.01f,
                "explicit diagonal aim remains eight-way");
        });

        Run("projectile spawn is above the player's feet at gun height", () =>
        {
            var game = new GameWorld();
            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));

            game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false), 0f);

            Assert(game.Projectiles.Count == 1, "horizontal aimed shot is spawned");
            Assert(game.Projectiles[0].Position.Y < game.PlayerPosition.Y,
                "horizontal projectile starts above the player's feet");
        });

        Run("movement advances by elapsed seconds rather than update-call count", () =>
        {
            var oneStep = new GameWorld();
            var fourSteps = new GameWorld();
            var input = new InputFrame(1, false, false, Vector2.Zero, false, false, false, false);

            oneStep.Update(input, 0.4f);
            for (var i = 0; i < 4; i++)
                fourSteps.Update(input, 0.1f);

            Assert(MathF.Abs(oneStep.PlayerPosition.X - fourSteps.PlayerPosition.X) < 0.01f,
                "equal elapsed time produces equal horizontal displacement");
            Assert(oneStep.PlayerPosition.Y == fourSteps.PlayerPosition.Y,
                "equal elapsed time preserves the same support surface");
        });

        Run("dash duration is measured in elapsed seconds", () =>
        {
            var game = new GameWorld();
            var input = new InputFrame(1, false, true, Vector2.Zero, false, false, false, false);
            var start = game.PlayerPosition;

            game.Update(input, 0.1f);
            var duringDash = game.PlayerPosition.X - start.X;
            game.Update(new InputFrame(1, false, false, Vector2.Zero, false, false, false, false), 0.1f);
            var beforeExit = game.PlayerPosition.X;
            game.Update(new InputFrame(1, false, false, Vector2.Zero, false, false, false, false), 0.02f);

            Assert(duringDash > GameWorld.PlayerSpeed * 0.1f,
                "dash moves faster than ordinary movement during its active window");
            Assert(MathF.Abs(game.PlayerPosition.X - beforeExit - GameWorld.PlayerSpeed * 0.02f) < 0.01f,
                "dash transitions to ordinary speed at its elapsed-time boundary");
        });

        Run("feet, facing, and muzzle contracts survive elapsed-time updates", () =>
        {
            var game = new GameWorld();
            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));
            var input = new InputFrame(-1, false, false, Vector2.Zero, false, false, false, false);

            game.Update(input, 0.2f);
            Assert(game.FacingDirection == -1, "horizontal movement updates facing");
            Assert(game.PlayerPosition.Y == RoomCatalog.Hub.Ground.Y,
                "horizontal movement keeps feet on the support surface");

            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(300, RoomCatalog.Hub.Ground.Y));
            game.Update(new InputFrame(0, false, false, Vector2.Zero, true, false, false, false), 0f);
            Assert(game.Projectiles.Count == 1, "neutral fire still produces a projectile");
            Assert(game.Projectiles[0].Position ==
                game.PlayerPosition + GameWorld.PlayerMuzzleOffset -
                Vector2.UnitX * GameWorld.PlayerMuzzleDistance,
                "neutral fire preserves the muzzle offset and facing direction");
        });

        Run("looping animation clips advance deterministically and wrap", () =>
        {
            var clip = new AnimationClip(
                "idle",
                new[]
                {
                    new AnimationFrame("idle_0"),
                    new AnimationFrame("idle_1"),
                    new AnimationFrame("idle_2")
                },
                10f,
                AnimationPlaybackMode.Loop);
            var clock = new AnimationClock();

            clock.Advance(0.25f, clip);

            Assert(clock.CurrentFrameIndex == 2, "quarter-second idle advance reaches the third frame");
            Assert(clock.CurrentFrame(clip).AssetKey == "idle_2", "current idle frame matches the clip key");
            Assert(!clock.IsComplete, "looping clips never complete");

            clock.Advance(0.1f, clip);
            Assert(clock.CurrentFrameIndex == 0, "looping clips wrap to their first frame");
        });

        Run("one-shot animation clips clamp on completion", () =>
        {
            var clip = new AnimationClip(
                "shoot",
                new[] { new AnimationFrame("shoot_0"), new AnimationFrame("shoot_1") },
                10f,
                AnimationPlaybackMode.OneShot);
            var clock = new AnimationClock();

            clock.Advance(0.1f, clip);
            Assert(clock.CurrentFrameIndex == 1, "one-shot advance reaches its final frame");
            Assert(!clock.IsComplete, "one-shot remains visible on its final frame for its duration");
            clock.Advance(0.1f, clip);
            Assert(clock.IsComplete, "one-shot clip reports completion at its final frame");

            clock.Advance(1f, clip);
            Assert(clock.CurrentFrameIndex == 1, "completed one-shot clips remain clamped");
            Assert(clock.CurrentFrame(clip).AssetKey == "shoot_1", "completed clip keeps its final asset");

            clock.Reset();
            Assert(clock.CurrentFrameIndex == 0 && clock.ElapsedSeconds == 0f && !clock.IsComplete,
                "reset makes a completed one-shot reusable");
        });

        Run("animation clocks advance by elapsed seconds including large deltas", () =>
        {
            var clip = new AnimationClip(
                "run",
                new[] { new AnimationFrame("run_0"), new AnimationFrame("run_1") },
                4f,
                AnimationPlaybackMode.Loop);
            var clock = new AnimationClock();

            clock.Advance(1.125f, clip);

            Assert(clock.CurrentFrameIndex == 0, "a full loop plus remainder returns to the first frame");
            Assert(MathF.Abs(clock.ElapsedSeconds - 0.125f) < 0.0001f,
                "large elapsed deltas preserve the fractional frame remainder");
        });

        Run("frontier player animation states preserve the stable contract", () =>
        {
            var expected = new[]
            {
                PlayerAnimationState.Idle,
                PlayerAnimationState.Run,
                PlayerAnimationState.Jump,
                PlayerAnimationState.Fall,
                PlayerAnimationState.Shoot,
                PlayerAnimationState.Reload,
                PlayerAnimationState.Hurt,
                PlayerAnimationState.Dash
            };

            Assert(Enum.GetValues<PlayerAnimationState>().SequenceEqual(expected),
                "player states contain idle, run, jump, fall, shoot, reload, hurt, and dash in contract order");
        });

        Run("frontier player clips map every authored frame and timing", () =>
        {
            var expected = new[]
            {
                (PresentationAnimationState.Idle, "player_idle", "idle", 4, 6f, AnimationPlaybackMode.Loop),
                (PresentationAnimationState.Run, "player_run", "run", 6, 12f, AnimationPlaybackMode.Loop),
                (PresentationAnimationState.Jump, "player_jump", "jump", 2, 8f, AnimationPlaybackMode.Loop),
                (PresentationAnimationState.Fall, "player_fall", "fall", 2, 8f, AnimationPlaybackMode.Loop),
                (PresentationAnimationState.Shoot, "player_shoot", "shoot", 3, 15f, AnimationPlaybackMode.OneShot),
                (PresentationAnimationState.Reload, "player_reload", "reload", 4, 4f / GameWorld.ReloadDuration, AnimationPlaybackMode.OneShot),
                (PresentationAnimationState.Hurt, "player_hurt", "hurt", 2, 10f, AnimationPlaybackMode.OneShot),
                (PresentationAnimationState.Dash, "player_dash", "dash", 3, 15f, AnimationPlaybackMode.OneShot)
            };

            Assert(FrontierAnimationCatalog.PlayerClips.Count == expected.Length,
                "the player catalog contains exactly the eight stable states");
            foreach (var (state, name, frameName, count, fps, mode) in expected)
            {
                var clip = FrontierAnimationCatalog.For(state);
                Assert(clip.Name == name, $"{state} retains its stable clip name");
                Assert(clip.Frames.Length == count, $"{state} has its authored frame count");
                Assert(MathF.Abs(clip.FramesPerSecond - fps) < 0.0001f, $"{state} has its authored timing");
                Assert(clip.PlaybackMode == mode, $"{state} has its authored playback mode");
                Assert(clip.Frames.Select(frame => frame.AssetKey).SequenceEqual(
                        Enumerable.Range(0, count)
                            .Select(index => $"Frontier/Player/{frameName}_{index}.png")),
                    $"{state} maps every exact Frontier player asset");
            }

            var reload = FrontierAnimationCatalog.For(PresentationAnimationState.Reload);
            Assert(MathF.Abs(reload.FrameDuration * reload.Frames.Length - GameWorld.ReloadDuration) < 0.0001f,
                "reload clip duration exactly matches the authoritative gameplay reload duration");
        });

        Run("frontier actor metadata preserves feet and effect anchors", () =>
        {
            Assert(FrontierAnimationCatalog.PlayerMetadata.SourceFeetAnchor == new Vector2(8, 13),
                "player sprites preserve the required source feet anchor");
            Assert(FrontierAnimationCatalog.BanditMetadata.SourceFeetAnchor == new Vector2(8, 13) &&
                   FrontierAnimationCatalog.WildlifeMetadata.SourceFeetAnchor == new Vector2(8, 13) &&
                   FrontierAnimationCatalog.PickupMetadata.SourceFeetAnchor == new Vector2(8, 13),
                "all Frontier actors expose the shared source feet anchor");
            Assert(FrontierAnimationCatalog.PlayerMetadata.SourceEffectAnchor == new Vector2(13, 7),
                "player metadata exposes the authored source muzzle anchor");
            Assert(FrontierAnimationCatalog.BanditMetadata.SourceEffectAnchor == new Vector2(14, 7),
                "bandit metadata exposes its authored muzzle anchor");
            Assert(FrontierAnimationCatalog.WildlifeMetadata.SourceEffectAnchor == new Vector2(14, 9),
                "wildlife metadata exposes its authored lunge effect anchor");
            Assert(FrontierAnimationCatalog.PickupMetadata.SourceEffectAnchor == new Vector2(8, 8),
                "pickup metadata exposes its authored center effect anchor");
        });

        Run("frontier enemy clips map both archetypes without inference", () =>
        {
            AssertEnemyClips(
                FrontierAnimationCatalog.BanditClips,
                "Bandit",
                new[]
                {
                    (PresentationAnimationState.BanditPatrol, "patrol", 4, 6f, AnimationPlaybackMode.Loop),
                    (PresentationAnimationState.BanditNotice, "notice", 2, 8f, AnimationPlaybackMode.OneShot),
                    (PresentationAnimationState.BanditAttack, "attack", 4, 12f, AnimationPlaybackMode.OneShot),
                    (PresentationAnimationState.EnemyDefeated, "defeated", 2, 6f, AnimationPlaybackMode.OneShot)
                });
            AssertEnemyClips(
                FrontierAnimationCatalog.WildlifeClips,
                "Wildlife",
                new[]
                {
                    (PresentationAnimationState.WildlifePatrol, "patrol", 4, 8f, AnimationPlaybackMode.Loop),
                    (PresentationAnimationState.WildlifeNotice, "notice", 2, 8f, AnimationPlaybackMode.OneShot),
                    (PresentationAnimationState.WildlifeLunge, "lunge", 4, 12f, AnimationPlaybackMode.OneShot),
                    (PresentationAnimationState.EnemyDefeated, "defeated", 2, 6f, AnimationPlaybackMode.OneShot)
                });

            var defeatedBandit = new EnemyState(
                "bandit", EnemyArchetype.Bandit, EnemyBehaviorState.Defeated, EnemyAttackPhase.None,
                Vector2.Zero, Vector2.Zero, 1, 0, false, 0, 0);
            var defeatedWildlife = defeatedBandit with
            {
                Id = "wildlife",
                Archetype = EnemyArchetype.Wildlife
            };
            Assert(FrontierAnimationCatalog.ForEnemy(defeatedBandit).Frames[0].AssetKey ==
                   "Frontier/Bandit/defeated_0.png",
                "defeated bandit selection remains archetype-specific");
            Assert(FrontierAnimationCatalog.ForEnemy(defeatedWildlife).Frames[0].AssetKey ==
                   "Frontier/Wildlife/defeated_0.png",
                "defeated wildlife selection remains archetype-specific");

            var clock = new PresentationAnimationClock();
            clock.Advance(0, defeatedWildlife);
            Assert(clock.CurrentClip == FrontierAnimationCatalog.WildlifeClips[PresentationAnimationState.EnemyDefeated] &&
                   clock.CurrentFrame().AssetKey == "Frontier/Wildlife/defeated_0.png",
                "snapshot-driven clocks retain the selected enemy archetype clip");
        });

        Run("frontier pickup clips cover currency health and ammo", () =>
        {
            var expected = new[]
            {
                (PickupType.Currency, "Currency"),
                (PickupType.Health, "Health"),
                (PickupType.ReserveAmmo, "Ammo")
            };

            Assert(FrontierAnimationCatalog.PickupClips.Count == expected.Length,
                "the pickup catalog contains exactly the three gameplay pickup types");
            foreach (var (type, folder) in expected)
            {
                var clip = FrontierAnimationCatalog.ForPickup(type);
                Assert(clip.Frames.Length == 4 && clip.PlaybackMode == AnimationPlaybackMode.Loop,
                    $"{type} uses a four-frame looping float clip");
                Assert(MathF.Abs(clip.FrameDuration - 1f / 6f) < 0.0001f,
                    $"{type} exposes the authored frame duration");
                Assert(clip.Frames.Select(frame => frame.AssetKey).SequenceEqual(
                        Enumerable.Range(0, 4)
                            .Select(index => $"Frontier/Pickup/{folder}/float_{index}.png")),
                    $"{type} maps every exact Frontier pickup asset");
            }

            var clock = new PresentationAnimationClock();
            clock.Advance(0.2f, PickupType.Currency);
            clock.Advance(0f, PickupType.Health);
            Assert(clock.CurrentFrameIndex == 0 &&
                   clock.CurrentFrame().AssetKey == "Frontier/Pickup/Health/float_0.png",
                "changing pickup type resets to the correct snapshot-selected clip");
        });

        Run("frontier clocks freeze externally and equivalent progression matches", () =>
        {
            var clip = FrontierAnimationCatalog.For(PresentationAnimationState.Run);
            var first = new AnimationClock();
            var second = new AnimationClock();

            foreach (var elapsed in new[] { 0.03f, 0f, 0.07f, 0.15f, 0.41f })
            {
                first.Advance(elapsed, clip);
                second.Advance(elapsed, clip);
            }

            Assert(first.CurrentFrameIndex == second.CurrentFrameIndex &&
                   first.ElapsedSeconds == second.ElapsedSeconds &&
                   first.CurrentFrame(clip) == second.CurrentFrame(clip),
                "equivalent clips and elapsed inputs produce equivalent frames");

            var frozenFrame = first.CurrentFrameIndex;
            var frozenElapsed = first.ElapsedSeconds;
            first.Advance(0f, clip);
            Assert(first.CurrentFrameIndex == frozenFrame && first.ElapsedSeconds == frozenElapsed,
                "not advancing presentation time leaves the clock externally freezeable");

            var oneShot = FrontierAnimationCatalog.For(PresentationAnimationState.Shoot);
            var oneShotClock = new AnimationClock();
            oneShotClock.Advance(oneShot.FrameDuration * oneShot.Frames.Length, oneShot);
            Assert(oneShotClock.IsComplete &&
                   oneShotClock.CurrentFrameIndex == oneShot.Frames.Length - 1,
                "Frontier one-shots complete and clamp on their final authored frame");
        });

        Run("wildlife active contact deals one hit and invulnerability blocks repeat damage", () =>
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

        Run("paused updates preserve the complete deterministic world snapshot", () =>
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

        Run("slot one selection is idempotent and slots two through ten are unavailable", () =>
        {
            var game = new GameWorld();
            Assert(game.SelectedWeaponSlot == 1, "the revolver starts selected in slot one");

            game.Update(new InputFrame(0, false, false, Vector2.UnitX, true, false, false, false), 0f);
            game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, true, false, false), 0f);
            var ammo = game.Ammo;
            var reloadTimer = GetField<float>(game, "reloadTimer");

            game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, false, true), 0f);
            game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, false, true), 0f);

            Assert(game.SelectedWeaponSlot == 1, "repeated slot-one selection remains on the revolver");
            Assert(game.Ammo == ammo && GetField<float>(game, "reloadTimer") == reloadTimer,
                "slot-one selection does not alter cylinder ammo or reload progress");

            var unavailableSlotInputs = typeof(InputFrame).GetProperties()
                .Where(property => property.Name.StartsWith("SelectSlot", StringComparison.Ordinal) &&
                                   property.Name != nameof(InputFrame.SelectSlot1Pressed))
                .ToArray();
            Assert(unavailableSlotInputs.Length == 0,
                "slots two through ten expose no active deterministic input");
        });

        Run("typed pickups are single-use and reserve ammo is cylinder-independent", () =>
        {
            var game = new GameWorld();

            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Hub.PickupDefinitions[0].Position);
            game.Update(default, 0f);
            game.Update(default, 0f);
            Assert(game.Currency == 1, "the hub currency pickup grants exactly one currency");

            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Hub.PickupDefinitions[1].Position);
            game.Update(default, 0f);
            Assert(game.Health == GameWorld.MaximumHealth && game.CollectedPickupCount == 1,
                "health at maximum remains available and is not marked collected");
            SetProperty(game, nameof(GameWorld.Health), GameWorld.MaximumHealth - 1);
            game.Update(default, 0f);
            game.Update(default, 0f);
            Assert(game.Health == GameWorld.MaximumHealth && game.CollectedPickupCount == 2,
                "the health pickup heals once and is then consumed");

            SetProperty(game, nameof(GameWorld.Room), RoomCatalog.Branch.Id);
            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Branch.PickupDefinitions[0].Position);
            var cylinderBeforeReserve = game.Ammo;
            game.Update(default, 0f);
            game.Update(default, 0f);
            Assert(game.ReserveAmmo == 1, "the branch reserve pickup grants exactly one reserve round");
            Assert(game.Ammo == cylinderBeforeReserve, "reserve ammo collection does not alter cylinder ammo");

            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Branch.PickupDefinitions[1].Position);
            game.Update(default, 0f);
            game.Update(default, 0f);
            Assert(game.Currency == 2, "the second currency pickup grants one additional currency");
            Assert(game.CollectedPickupCount == 4, "all four successful pickups are collected exactly once");

            SetProperty(game, nameof(GameWorld.Ammo), 1);
            game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, true, false, false), 0f);
            game.Update(default, GameWorld.ReloadDuration);
            Assert(game.Ammo == 6 && game.ReserveAmmo == 1,
                "reload restores only the cylinder and leaves reserve ammo unchanged");
        });

        Run("grounded horizontal contact collects pickups at but not beyond the radius", () =>
        {
            var game = new GameWorld();
            var pickup = RoomCatalog.Hub.PickupDefinitions[0];
            var approach = new Vector2(pickup.Position.X - GameWorld.PlayerSpeed * 0.1f, RoomCatalog.Hub.Ground.Y);
            SetProperty(game, nameof(GameWorld.PlayerPosition), approach);

            game.Update(default, 0f);
            Assert(game.Currency == 0 && game.CollectedPickupCount == 0,
                "a grounded player beyond the pickup radius does not collect it");

            game.Update(new InputFrame(1, false, false, Vector2.Zero, false, false, false, false), 0.1f);

            Assert(game.PlayerPosition == new Vector2(pickup.Position.X, RoomCatalog.Hub.Ground.Y),
                "horizontal movement reaches the pickup directly beneath its anchor");
            Assert(Vector2.Distance(game.PlayerPosition, pickup.Position) == GameWorld.PickupRadius,
                "grounded contact is exactly at the pickup radius");
            Assert(game.Currency == 1 && game.CollectedPickupCount == 1,
                "grounded contact at the pickup radius collects it");
        });

        Run("runtime pickups economy shortcut and checkpoint survive death but not a new world", () =>
        {
            var game = new GameWorld();
            SetProperty(game, nameof(GameWorld.Health), 2);

            foreach (var pickup in RoomCatalog.Hub.PickupDefinitions)
            {
                SetProperty(game, nameof(GameWorld.PlayerPosition), pickup.Position);
                game.Update(default, 0f);
            }

            SetProperty(game, nameof(GameWorld.Room), RoomCatalog.Branch.Id);
            foreach (var pickup in RoomCatalog.Branch.PickupDefinitions)
            {
                SetProperty(game, nameof(GameWorld.PlayerPosition), pickup.Position);
                game.Update(default, 0f);
            }

            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Branch.Checkpoint);
            InvokePrivate(game, "Interact");
            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Branch.Shortcut);
            InvokePrivate(game, "Interact");

            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Hub.Checkpoint);
            InvokePrivate(game, "Respawn");

            Assert(game.Currency == 2 && game.ReserveAmmo == 1 && game.CollectedPickupCount == 4,
                "death preserves collected pickups and economy counters");
            Assert(game.ShortcutUnlocked && game.CheckpointRoom == RoomCatalog.Hub.Id &&
                   game.CheckpointPosition == RoomCatalog.Hub.Checkpoint,
                "death preserves shortcut and coherent active checkpoint state");

            var fresh = new GameWorld();
            Assert(fresh.Currency == 0 && fresh.ReserveAmmo == 0 && fresh.CollectedPickupCount == 0,
                "a new world resets runtime pickup and economy progress");
            Assert(!fresh.ShortcutUnlocked && !fresh.Completed &&
                   fresh.CheckpointRoom == RoomCatalog.Hub.Id &&
                   fresh.CheckpointPosition == RoomCatalog.Hub.Checkpoint,
                "a new world resets runtime shortcut, objective, and checkpoint progress");
            Assert(fresh.AvailablePickups.Count() == RoomCatalog.Hub.Pickups.Length,
                "a new world restores the current room's pickup availability");
        });

        Run("checkpoint and respawn restore room context and spawn point", () =>
        {
            var game = new GameWorld();
            SetProperty(game, nameof(GameWorld.Room), 1);
            SetProperty(game, nameof(GameWorld.CheckpointRoom), RoomCatalog.Branch.Id);
            SetProperty(game, nameof(GameWorld.CheckpointPosition), new Vector2(760, 480));
            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(760, 480));
            InvokePrivate(game, "Respawn");

            Assert(game.Health == 3, "respawn restores full health after a lethal hit");
            Assert(game.PlayerPosition == game.CheckpointPosition, "respawn returns the player to the active checkpoint");
            Assert(game.Room == 1, "respawn restores the branch-room context");
        });

        Run("shortcut return establishes a coherent hub checkpoint for later death", () =>
        {
            var game = new GameWorld();
            SetProperty(game, nameof(GameWorld.Room), RoomCatalog.Branch.Id);
            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Branch.Checkpoint);
            InvokePrivate(game, "Interact");

            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Branch.Shortcut);
            InvokePrivate(game, "Interact");

            Assert(game.Room == RoomCatalog.Hub.Id, "shortcut return enters the hub");
            Assert(game.PlayerPosition == RoomCatalog.Hub.Shortcut, "shortcut return preserves the authored hub landing");
            Assert(game.CheckpointPosition == RoomCatalog.Hub.Checkpoint,
                "shortcut return establishes the authored hub checkpoint position");

            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Hub.Checkpoint);
            InvokePrivate(game, "Respawn");

            Assert(game.Room == RoomCatalog.Hub.Id,
                "death after shortcut return respawns in the checkpoint's hub room");
            Assert(game.PlayerPosition == RoomCatalog.Hub.Checkpoint,
                "death after shortcut return respawns at the coherent hub checkpoint position");
        });

        Run("hub objective completion requires interaction within 42 units of the shortcut", () =>
        {
            var game = new GameWorld();
            SetProperty(game, nameof(GameWorld.ShortcutUnlocked), true);

            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Hub.Shortcut - new Vector2(43, 0));
            InvokePrivate(game, "Interact");
            Assert(!game.Completed, "interaction outside the 42-unit radius does not complete the slice");

            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Hub.Shortcut - new Vector2(41, 0));
            InvokePrivate(game, "Interact");
            Assert(game.Completed, "interaction inside the 42-unit radius completes the slice");

            var completed = CaptureWorld(game);
            game.Update(new InputFrame(-1, true, true, -Vector2.UnitX, true, true, true, false), 5f);
            Assert(CaptureWorld(game) == completed, "completed state is terminal and freezes simulation");
        });

        Run("shortcut unlock and hub return complete the first-slice objective", () =>
        {
            var game = new GameWorld();

            SetProperty(game, nameof(GameWorld.Room), 1);
            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(1440, 480));
            SetProperty(game, nameof(GameWorld.ShortcutUnlocked), false);
            InvokePrivate(game, "Interact");

            Assert(game.Room == 0, "shortcut interaction returns the player to the hub room");
            Assert(game.ShortcutUnlocked, "reaching the shortcut unlocks the return route");
            Assert(game.PlayerPosition == RoomCatalog.Hub.Shortcut, "shortcut return lands on the hub-side exit tile");

            SetProperty(game, nameof(GameWorld.Room), 0);
            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Hub.Shortcut);
            SetProperty(game, nameof(GameWorld.ShortcutUnlocked), true);
            InvokePrivate(game, "Interact");

            Assert(game.Completed, "re-entering the return lane completes the objective");
        });

        Console.WriteLine("Cowbania.Core.Tests: PASS");
    }

    static void Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine($"[PASS] {name}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{name}: {ex.Message}", ex);
        }
    }

    static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    static void AssertEnemyClips(
        IReadOnlyDictionary<PresentationAnimationState, AnimationClip> clips,
        string actorFolder,
        IEnumerable<(PresentationAnimationState State, string FrameName, int Count, float Fps, AnimationPlaybackMode Mode)> expected)
    {
        var definitions = expected.ToArray();
        Assert(clips.Count == definitions.Length, $"{actorFolder} has exactly its four stable clips");
        foreach (var (state, frameName, count, fps, mode) in definitions)
        {
            var clip = clips[state];
            Assert(clip.Frames.Length == count, $"{actorFolder} {frameName} has its authored frame count");
            Assert(MathF.Abs(clip.FramesPerSecond - fps) < 0.0001f,
                $"{actorFolder} {frameName} has its authored timing");
            Assert(clip.PlaybackMode == mode, $"{actorFolder} {frameName} has its authored playback mode");
            Assert(clip.Frames.Select(frame => frame.AssetKey).SequenceEqual(
                    Enumerable.Range(0, count)
                        .Select(index => $"Frontier/{actorFolder}/{frameName}_{index}.png")),
                $"{actorFolder} {frameName} maps every exact Frontier asset");
        }
    }

    static bool IsSupported(RoomDefinition room, Vector2 position)
    {
        return room.Solids.Any(s =>
            MathF.Abs(position.Y - s.Y) < 0.01f &&
            position.X >= s.X &&
            position.X <= s.Right);
    }

    static void SetField<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field is null)
        {
            throw new InvalidOperationException($"Missing field '{fieldName}' on {target.GetType().Name}.");
        }

        field.SetValue(target, value);
    }

    static void SetProperty<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (property is null || property.SetMethod is null)
        {
            throw new InvalidOperationException($"Missing property '{propertyName}' on {target.GetType().Name}.");
        }

        property.SetValue(target, value);
    }

    static void InvokePrivate(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (method is null)
        {
            throw new InvalidOperationException($"Missing method '{methodName}' on {target.GetType().Name}.");
        }

        method.Invoke(target, null);
    }

    static WorldSnapshot CaptureWorld(GameWorld game) => new(
        game.PlayerPosition,
        game.PlayerVelocity,
        game.CheckpointPosition,
        game.CheckpointRoom,
        game.Health,
        game.Ammo,
        game.ReserveAmmo,
        game.Currency,
        game.SelectedWeaponSlot,
        game.CollectedPickupCount,
        game.Room,
        game.AimDirection,
        game.FacingDirection,
        game.IsPaused,
        game.ShortcutUnlocked,
        game.Completed,
        game.Projectiles.ToArray(),
        game.Enemies.ToArray(),
        GetField<float>(game, "dashTimer"),
        GetField<float>(game, "dashCooldown"),
        GetField<float>(game, "fireTimer"),
        GetField<float>(game, "reloadTimer"),
        GetField<float>(game, "invulnerabilityTimer"));

    static T GetField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field is null)
        {
            throw new InvalidOperationException($"Missing field '{fieldName}' on {target.GetType().Name}.");
        }

        return (T)field.GetValue(target)!;
    }

    readonly record struct WorldSnapshot(
        Vector2 PlayerPosition,
        Vector2 PlayerVelocity,
        Vector2 CheckpointPosition,
        int CheckpointRoom,
        int Health,
        int Ammo,
        int ReserveAmmo,
        int Currency,
        int SelectedWeaponSlot,
        int CollectedPickupCount,
        int Room,
        Vector2 AimDirection,
        int FacingDirection,
        bool IsPaused,
        bool ShortcutUnlocked,
        bool Completed,
        ProjectileState[] Projectiles,
        EnemyState[] Enemies,
        float DashTimer,
        float DashCooldown,
        float FireTimer,
        float ReloadTimer,
        float InvulnerabilityTimer)
    {
        public bool Equals(WorldSnapshot other) =>
            PlayerPosition == other.PlayerPosition &&
            PlayerVelocity == other.PlayerVelocity &&
            CheckpointPosition == other.CheckpointPosition &&
            CheckpointRoom == other.CheckpointRoom &&
            Health == other.Health &&
            Ammo == other.Ammo &&
            ReserveAmmo == other.ReserveAmmo &&
            Currency == other.Currency &&
            SelectedWeaponSlot == other.SelectedWeaponSlot &&
            CollectedPickupCount == other.CollectedPickupCount &&
            Room == other.Room &&
            AimDirection == other.AimDirection &&
            FacingDirection == other.FacingDirection &&
            IsPaused == other.IsPaused &&
            ShortcutUnlocked == other.ShortcutUnlocked &&
            Completed == other.Completed &&
            Projectiles.SequenceEqual(other.Projectiles) &&
            Enemies.SequenceEqual(other.Enemies) &&
            DashTimer == other.DashTimer &&
            DashCooldown == other.DashCooldown &&
            FireTimer == other.FireTimer &&
            ReloadTimer == other.ReloadTimer &&
            InvulnerabilityTimer == other.InvulnerabilityTimer;

        public override int GetHashCode() => HashCode.Combine(
            PlayerPosition, PlayerVelocity, CheckpointPosition, Health, Ammo, Room, AimDirection, FacingDirection);
    }
}