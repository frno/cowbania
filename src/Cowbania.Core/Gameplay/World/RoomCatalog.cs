using System.Collections.Immutable;
using System.Numerics;
using Cowbania.Core.Gameplay.Enemies;
using Cowbania.Core.Gameplay.Pickups;
using Cowbania.Core.Gameplay.World.Geometry;

namespace Cowbania.Core.Gameplay.World;

public static class RoomCatalog
{
    public static readonly RoomDefinition Hub = new(
        0, "Hub", new RoomRect(0, 0, 1600, 576),
        ImmutableArray.Create(
            new RoomRect(0, 480, 1600, 96),
            new RoomRect(250, 380, 220, 24),
            new RoomRect(650, 320, 240, 24)),
        new Vector2(80, 480), new Vector2(80, 480), new Vector2(1440, 480),
        ImmutableArray.Create(
            new EnemyDefinition("hub-bandit-0", EnemyArchetype.Bandit, new Vector2(520, 480), 120),
            new EnemyDefinition("hub-wildlife-1", EnemyArchetype.Wildlife, new Vector2(1040, 480), 120),
            new EnemyDefinition("hub-armadillo-2", EnemyArchetype.DynamiteArmadillo, new Vector2(760, 320), 90)),
        ImmutableArray.Create(
            new PickupDefinition("hub-currency", new Vector2(1120, 448), PickupType.Currency),
            new PickupDefinition("hub-health", new Vector2(1280, 448), PickupType.Health)));

    public static readonly RoomDefinition Branch = new(
        1, "Branch", new RoomRect(0, 0, 1600, 576),
        ImmutableArray.Create(
            new RoomRect(0, 480, 1600, 96),
            new RoomRect(180, 370, 240, 24),
            new RoomRect(760, 290, 260, 24)),
        new Vector2(40, 480), new Vector2(760, 480), new Vector2(1440, 480),
        ImmutableArray.Create(
            new EnemyDefinition("branch-wildlife-0", EnemyArchetype.Wildlife, new Vector2(520, 480), 120),
            new EnemyDefinition("branch-bandit-1", EnemyArchetype.Bandit, new Vector2(1120, 480), 120),
            new EnemyDefinition("branch-snake-2", EnemyArchetype.SidewinderSnake, new Vector2(880, 290), 0)),
        ImmutableArray.Create(
            new PickupDefinition("branch-reserve-ammo", new Vector2(760, 448), PickupType.ReserveAmmo),
            new PickupDefinition("branch-currency", new Vector2(1440, 448), PickupType.Currency)));

    public static RoomDefinition ForId(int id) => id == Branch.Id ? Branch : Hub;
}
