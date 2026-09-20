using System.Numerics;

namespace Cowbania.Core.Gameplay.Pickups;

internal static class PickupSystem
{
    internal static void Collect(GameWorldState state)
    {
        foreach (var pickup in state.CurrentRoom.Pickups)
        {
            if (state.CollectedPickupIds.Contains(pickup.Id) ||
                Vector2.Distance(state.PlayerPosition, pickup.Position) > GameWorld.PickupRadius)
                continue;

            if (pickup.Type == PickupType.Health && state.Health >= GameWorld.MaximumHealth)
                continue;

            switch (pickup.Type)
            {
                case PickupType.Coin:
                    state.Score += GameWorld.PointsPerCoin;
                    break;
                case PickupType.Health:
                    state.Health = Math.Min(GameWorld.MaximumHealth, state.Health + 1);
                    break;
                case PickupType.ReserveAmmo:
                    state.ReserveAmmo++;
                    break;
            }

            state.CollectedPickupIds.Add(pickup.Id);
        }
    }
}
