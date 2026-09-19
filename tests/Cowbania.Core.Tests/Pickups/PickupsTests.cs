namespace Cowbania.Core.Tests.Pickups;

internal static class PickupsTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("typed pickups are single-use and reserve ammo is cylinder-independent", () =>
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
            yield return new TestCase("grounded horizontal contact collects pickups at but not beyond the radius", () =>
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
            yield return new TestCase("runtime pickups economy shortcut and checkpoint survive death but not a new world", () =>
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
        }
    }
}
