namespace Cowbania.Core.Tests.World;

internal static class WorldTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("hub and branch expose distinct stage geometry", () =>
            {
                Assert(RoomCatalog.Hub.Id != RoomCatalog.Branch.Id, "hub and branch have distinct room ids");
                            Assert(RoomCatalog.Hub.Name != RoomCatalog.Branch.Name, "hub and branch have distinct names");
                            Assert(!RoomCatalog.Hub.Solids.SequenceEqual(RoomCatalog.Branch.Solids), "hub and branch solids are distinct");
                            Assert(RoomCatalog.Hub.Solids[1] != RoomCatalog.Branch.Solids[1], "raised-platform geometry differs between rooms");
            });
            yield return new TestCase("room visual selection remains keyed by RoomCatalog ids", () =>
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
            yield return new TestCase("catalog surfaces provide the renderer's collision-aligned stage contract", () =>
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
            yield return new TestCase("expanded frontier keeps its authored progression anchors", () =>
            {
                var expected = new[]
                            {
                                (RoomCatalog.Hub, 0, "Dustwind Crossing", new RoomRect(0, 0, 5200, 576), new Vector2(80, 480), new Vector2(80, 480), new Vector2(4960, 480)),
                                (RoomCatalog.Branch, 1, "Rattlesnake Run", new RoomRect(0, 0, 6400, 576), new Vector2(40, 480), new Vector2(2784, 360), new Vector2(6200, 480))
                            };

                            foreach (var (room, id, name, bounds, spawn, checkpoint, shortcut) in expected)
                            {
                                Assert(room.Id == id && room.Name == name, $"{name} identity remains authored");
                                Assert(room.Bounds == bounds, $"{name} bounds remain authored");
                                Assert(room.Spawn == spawn && room.Checkpoint == checkpoint && room.Shortcut == shortcut,
                                    $"{name} interaction anchors remain authored");
                            }

                            Assert(RoomCatalog.Hub.Solids.Length == 14 && RoomCatalog.Branch.Solids.Length == 18,
                                "the expanded rooms retain all authored ground and platform surfaces");
                            Assert(RoomCatalog.Hub.EnemyDefinitions.Length == 9 && RoomCatalog.Branch.EnemyDefinitions.Length == 13,
                                "the expanded rooms retain their complete encounter sequences");
                            Assert(RoomCatalog.Hub.Pickups.Length == 5 && RoomCatalog.Branch.Pickups.Length == 7,
                                "the expanded rooms retain their optional reward trail");
            });
            yield return new TestCase("expanded level content stays supported unique and traversable", () =>
            {
                var rooms = new[] { RoomCatalog.Hub, RoomCatalog.Branch };
                            Assert(rooms.Sum(room => room.Bounds.Width) >= 11000,
                                "the connected frontier spans at least eleven thousand horizontal units");

                            var enemyIds = rooms.SelectMany(room => room.EnemyDefinitions).Select(enemy => enemy.Id).ToArray();
                            var pickupIds = rooms.SelectMany(room => room.Pickups).Select(pickup => pickup.Id).ToArray();
                            Assert(enemyIds.Distinct(StringComparer.Ordinal).Count() == enemyIds.Length,
                                "every expanded encounter has a globally unique stable id");
                            Assert(pickupIds.Distinct(StringComparer.Ordinal).Count() == pickupIds.Length,
                                "every expanded reward has a globally unique stable id");

                            foreach (var room in rooms)
                            {
                                var supportPoints = room.EnemySpawns
                                    .Concat(new[] { room.Spawn, room.Checkpoint, room.Shortcut });
                                foreach (var point in supportPoints)
                                    Assert(room.Solids.Any(solid => point.Y == solid.Y && point.X >= solid.X && point.X <= solid.Right),
                                        $"{room.Name} anchor {point} stands on authored collision geometry");

                                foreach (var platform in room.Solids.Where(solid => solid.Height == 24))
                                {
                                    var hasApproach = room.Solids.Any(support =>
                                        support != platform && MathF.Abs(support.Y - platform.Y) <= 152 &&
                                        support.X <= platform.Right + 160 && support.Right >= platform.X - 160);
                                    Assert(hasApproach, $"{room.Name} platform at {platform.X},{platform.Y} has a reachable adjacent surface");
                                }

                                foreach (var pickup in room.Pickups)
                                    Assert(room.Solids.Any(solid =>
                                            pickup.Position.Y == solid.Y - GameWorld.PickupHoverHeight &&
                                            pickup.Position.X >= solid.X && pickup.Position.X <= solid.Right),
                                        $"{room.Name} pickup {pickup.Id} floats at walking height above a real support surface");
                            }
            });
            yield return new TestCase("main route contains mandatory gaps and major elevation changes", () =>
            {
                foreach (var room in new[] { RoomCatalog.Hub, RoomCatalog.Branch })
                            {
                                var floorSegments = room.Solids
                                    .Where(solid => solid.Height > 24)
                                    .OrderBy(solid => solid.X)
                                    .ToArray();
                                Assert(floorSegments.Length >= 3,
                                    $"{room.Name} has separated recovery and combat plateaus");
                                Assert(floorSegments.Zip(floorSegments.Skip(1))
                                        .Count(pair => pair.First.Right < pair.Second.X) >= 2,
                                    $"{room.Name} has at least two floorless traversal zones");
                                Assert(room.Solids.Min(solid => solid.Y) <= 248,
                                    $"{room.Name} forces the route onto a substantially higher plateau");

                                var straightLine = new GameWorld();
                                SetProperty(straightLine, nameof(GameWorld.Room), room.Id);
                                SetProperty(straightLine, nameof(GameWorld.PlayerPosition), room.Spawn);
                                SetProperty(straightLine, nameof(GameWorld.Health), GameWorld.MaximumHealth);
                                for (var step = 0; step < 50; step++)
                                    straightLine.Update(new InputFrame(1, false, false, Vector2.UnitX, false, false, false, false), 0.05f);

                                Assert(straightLine.Health < GameWorld.MaximumHealth,
                                    $"holding right without jumping falls in {room.Name}");
                                Assert(straightLine.PlayerPosition.X < room.Bounds.Right / 2f,
                                    $"holding right cannot bypass {room.Name}'s platform route");
                            }
            });
            yield return new TestCase("presentation-facing updates do not mutate collision authority", () =>
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
            yield return new TestCase("spawn checkpoint and shortcut positions are supported by room geometry", () =>
            {
                foreach (var room in new[] { RoomCatalog.Hub, RoomCatalog.Branch })
                            {
                                Assert(IsSupported(room, room.Spawn), $"{room.Name} spawn is supported");
                                Assert(IsSupported(room, room.Checkpoint), $"{room.Name} checkpoint is supported");
                                Assert(IsSupported(room, room.Shortcut), $"{room.Name} shortcut is supported");
                            }
            });
            yield return new TestCase("slot one selection is idempotent and slots two through ten are unavailable", () =>
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
            yield return new TestCase("checkpoint and respawn restore room context and spawn point", () =>
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
            yield return new TestCase("shortcut return establishes a coherent hub checkpoint for later death", () =>
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
            yield return new TestCase("hub objective completion requires interaction within 42 units of the shortcut", () =>
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
            yield return new TestCase("shortcut unlock and hub return complete the first-slice objective", () =>
            {
                var game = new GameWorld();

                            SetProperty(game, nameof(GameWorld.Room), 1);
                            SetProperty(game, nameof(GameWorld.PlayerPosition), RoomCatalog.Branch.Shortcut);
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
        }
    }
}
