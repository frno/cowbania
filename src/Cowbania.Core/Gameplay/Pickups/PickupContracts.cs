using System.Numerics;

namespace Cowbania.Core.Gameplay.Pickups;

public enum PickupType
{
    Coin,
    Health,
    ReserveAmmo
}

public readonly record struct PickupDefinition(string Id, Vector2 Position, PickupType Type);
