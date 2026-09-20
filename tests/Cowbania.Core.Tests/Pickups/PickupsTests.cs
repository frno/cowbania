namespace Cowbania.Core.Tests.Pickups;

internal static class PickupsTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("coins award score once and reserve ammo is cylinder-independent", () =>
            {
                var game = new GameWorld();
                            var hubCoin = RoomCatalog.Hub.Pickups.Single(pickup => pickup.Id == "hub-coin-town-edge");
                            var hubHealth = RoomCatalog.Hub.Pickups.Single(pickup => pickup.Id == "hub-health");
                            var branchAmmo = RoomCatalog.Branch.Pickups.Single(pickup => pickup.Id == "branch-reserve-ammo");
                            var branchCoin = RoomCatalog.Branch.Pickups.Single(pickup => pickup.Id == "branch-coin-vulture-crown");

                            SetProperty(game, nameof(GameWorld.PlayerPosition), hubCoin.Position);
                            game.Update(default, 0f);
                            game.Update(default, 0f);
                            Assert(game.Score == GameWorld.PointsPerCoin && game.CoinsCollected == 1,
                                "the hub coin grants points once and advances coin progress once");
                            Assert(game.TotalCoins == 23, "the complete run includes every coin across both rooms");
                            Assert(game.CurrentRoomCoinsCollected == 1 && game.CurrentRoomTotalCoins == 9,
                                "hub coin progress reports only Dustwind Crossing coins");

                            SetProperty(game, nameof(GameWorld.PlayerPosition), hubHealth.Position);
                            game.Update(default, 0f);
                            Assert(game.Health == GameWorld.MaximumHealth && game.CollectedPickupCount == 1,
                                "health at maximum remains available and is not marked collected");
                            SetProperty(game, nameof(GameWorld.Health), GameWorld.MaximumHealth - 1);
                            game.Update(default, 0f);
                            game.Update(default, 0f);
                            Assert(game.Health == GameWorld.MaximumHealth && game.CollectedPickupCount == 2,
                                "the health pickup heals once and is then consumed");

                            SetProperty(game, nameof(GameWorld.Room), RoomCatalog.Branch.Id);
                            Assert(game.CurrentRoomCoinsCollected == 0 && game.CurrentRoomTotalCoins == 14,
                                "entering Rattlesnake Run switches the visible progress to its fourteen coins");
                            SetProperty(game, nameof(GameWorld.PlayerPosition), branchAmmo.Position);
                            var cylinderBeforeReserve = game.Ammo;
                            game.Update(default, 0f);
                            game.Update(default, 0f);
                            Assert(game.ReserveAmmo == 1, "the branch reserve pickup grants exactly one reserve round");
                            Assert(game.Ammo == cylinderBeforeReserve, "reserve ammo collection does not alter cylinder ammo");

                            SetProperty(game, nameof(GameWorld.PlayerPosition), branchCoin.Position);
                            game.Update(default, 0f);
                            game.Update(default, 0f);
                            Assert(game.Score == GameWorld.PointsPerCoin * 2 && game.CoinsCollected == 2,
                                "the second coin grants another score increment and advances progress");
                            Assert(game.CurrentRoomCoinsCollected == 1 && game.CurrentRoomTotalCoins == 14,
                                "branch progress counts only the coin collected in the current room");
                            Assert(game.CollectedPickupCount == 4, "all four successful pickups are collected exactly once");

                            SetProperty(game, nameof(GameWorld.Ammo), 1);
                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, true, false, false), 0f);
                            game.Update(default, GameWorld.ReloadDuration);
                            Assert(game.Ammo == 6 && game.ReserveAmmo == 1,
                                "reload restores only the cylinder and leaves reserve ammo unchanged");
            });
            yield return new TestCase("grounded players collect lowered pickups without exact alignment", () =>
            {
                var game = new GameWorld();
                            var pickup = RoomCatalog.Hub.Pickups.Single(item => item.Id == "hub-coin-town-edge");
                            var approach = new Vector2(pickup.Position.X - 40, RoomCatalog.Hub.Ground.Y);
                            SetProperty(game, nameof(GameWorld.PlayerPosition), approach);

                            game.Update(default, 0f);
                            Assert(game.Score == 0 && game.CoinsCollected == 0 && game.CollectedPickupCount == 0,
                                "a grounded player beyond the pickup radius does not collect it");

                            game.Update(new InputFrame(1, false, false, Vector2.Zero, false, false, false, false), 0.05f);

                            Assert(game.PlayerPosition.X < pickup.Position.X,
                                "the player collects the item before standing directly beneath it");
                            Assert(Vector2.Distance(game.PlayerPosition, pickup.Position) < GameWorld.PickupRadius,
                                "the lowered pickup comfortably overlaps the grounded collection radius");
                            Assert(game.Score == GameWorld.PointsPerCoin && game.CoinsCollected == 1 && game.CollectedPickupCount == 1,
                                "ordinary grounded movement collects the lowered pickup");
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

                            Assert(game.Score == GameWorld.PointsPerCoin * game.TotalCoins &&
                                   game.CoinsCollected == game.TotalCoins &&
                                   game.ReserveAmmo == 3 && game.CollectedPickupCount == 27,
                                "death preserves score, coin progress, collected pickups, and reserve ammo");
                            Assert(game.ShortcutUnlocked && game.CheckpointRoom == RoomCatalog.Hub.Id &&
                                   game.CheckpointPosition == RoomCatalog.Hub.Checkpoint,
                                "death preserves shortcut and coherent active checkpoint state");

                            var fresh = new GameWorld();
                            Assert(fresh.Score == 0 && fresh.CoinsCollected == 0 && fresh.TotalCoins == 23 &&
                                   fresh.ReserveAmmo == 0 && fresh.CollectedPickupCount == 0,
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
