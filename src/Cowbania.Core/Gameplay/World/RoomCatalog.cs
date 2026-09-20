using System.Collections.Immutable;
using System.Numerics;
using Cowbania.Core.Gameplay.Enemies;
using Cowbania.Core.Gameplay.Pickups;
using Cowbania.Core.Gameplay.World.Geometry;

namespace Cowbania.Core.Gameplay.World;

public static class RoomCatalog
{
    public static readonly RoomDefinition Hub = new(
        0, "Dustwind Crossing", new RoomRect(0, 0, 5200, 576),
        ImmutableArray.Create(
            // A forgiving town opening teaches jumping before the trail breaks apart.
            new RoomRect(0, 480, 1240, 96),
            new RoomRect(250, 380, 220, 24),
            new RoomRect(650, 320, 240, 24),
            new RoomRect(960, 392, 192, 24),
            // Broken aqueduct: the street ends and three jumps become mandatory.
            new RoomRect(1200, 400, 240, 24),
            new RoomRect(1450, 320, 240, 24),
            new RoomRect(1730, 400, 240, 24),
            // Cattle-run recovery lane.
            new RoomRect(1980, 480, 850, 96),
            // Telegraph ridge: a mandatory climb to a high combat plateau.
            new RoomRect(2760, 400, 220, 24),
            new RoomRect(3000, 320, 220, 24),
            new RoomRect(3260, 248, 220, 24),
            new RoomRect(3540, 320, 240, 24),
            new RoomRect(3800, 400, 200, 24),
            // Windmill gate landing.
            new RoomRect(4020, 480, 1180, 96)),
        new Vector2(80, 480), new Vector2(80, 480), new Vector2(4960, 480),
        ImmutableArray.Create(
            new EnemyDefinition("hub-bandit-0", EnemyArchetype.Bandit, new Vector2(520, 480), 120),
            new EnemyDefinition("hub-wildlife-1", EnemyArchetype.Wildlife, new Vector2(1040, 480), 120),
            new EnemyDefinition("hub-armadillo-aqueduct", EnemyArchetype.DynamiteArmadillo, new Vector2(1310, 400), 90),
            new EnemyDefinition("hub-bandit-water-tower", EnemyArchetype.Bandit, new Vector2(1570, 320), 90, -1),
            new EnemyDefinition("hub-wildlife-aqueduct-exit", EnemyArchetype.Wildlife, new Vector2(1850, 400), 90),
            new EnemyDefinition("hub-wildlife-cattle-run", EnemyArchetype.Wildlife, new Vector2(2400, 480), 180),
            new EnemyDefinition("hub-bandit-ridge", EnemyArchetype.Bandit, new Vector2(3360, 248), 80, -1),
            new EnemyDefinition("hub-armadillo-ridge-descent", EnemyArchetype.DynamiteArmadillo, new Vector2(3650, 320), 90),
            new EnemyDefinition("hub-wildlife-gate", EnemyArchetype.Wildlife, new Vector2(4400, 480), 160, -1)),
        ImmutableArray.Create(
            new PickupDefinition("hub-currency", new Vector2(1120, 448), PickupType.Currency),
            new PickupDefinition("hub-health", new Vector2(1180, 448), PickupType.Health),
            new PickupDefinition("hub-currency-telegraph", new Vector2(3360, 216), PickupType.Currency),
            new PickupDefinition("hub-reserve-ammo-windmill", new Vector2(3650, 288), PickupType.ReserveAmmo),
            new PickupDefinition("hub-health-branch-gate", new Vector2(4760, 448), PickupType.Health)));

    public static readonly RoomDefinition Branch = new(
        1, "Rattlesnake Run", new RoomRect(0, 0, 6400, 576),
        ImmutableArray.Create(
            // Mine-mouth opening and optional lookout shelves.
            new RoomRect(0, 480, 1200, 96),
            new RoomRect(180, 370, 240, 24),
            new RoomRect(760, 290, 260, 24),
            new RoomRect(1060, 392, 192, 24),
            // Vulture span climbs 240 pixels above the canyon floor.
            new RoomRect(1150, 400, 220, 24),
            new RoomRect(1400, 320, 220, 24),
            new RoomRect(1660, 240, 220, 24),
            new RoomRect(1940, 320, 220, 24),
            new RoomRect(2200, 400, 220, 24),
            // Oasis mesa is a long elevated checkpoint and combat arena.
            new RoomRect(2420, 360, 780, 48),
            new RoomRect(3170, 420, 200, 24),
            new RoomRect(3370, 480, 760, 96),
            // Dynamite terraces force a second ascent and descent.
            new RoomRect(4060, 400, 220, 24),
            new RoomRect(4310, 320, 220, 24),
            new RoomRect(4570, 240, 220, 24),
            new RoomRect(4860, 320, 220, 24),
            new RoomRect(5140, 400, 220, 24),
            // Gallows run to the shortcut.
            new RoomRect(5360, 480, 1040, 96)),
        new Vector2(40, 480), new Vector2(2784, 360), new Vector2(6200, 480),
        ImmutableArray.Create(
            new EnemyDefinition("branch-wildlife-0", EnemyArchetype.Wildlife, new Vector2(520, 480), 120),
            new EnemyDefinition("branch-bandit-1", EnemyArchetype.Bandit, new Vector2(1120, 480), 120),
            new EnemyDefinition("branch-snake-vulture-span", EnemyArchetype.SidewinderSnake, new Vector2(1280, 400), 0),
            new EnemyDefinition("branch-bandit-high-span", EnemyArchetype.Bandit, new Vector2(1510, 320), 80, -1),
            new EnemyDefinition("branch-armadillo-canyon-crown", EnemyArchetype.DynamiteArmadillo, new Vector2(1770, 240), 80),
            new EnemyDefinition("branch-wildlife-span-exit", EnemyArchetype.Wildlife, new Vector2(2050, 320), 80, -1),
            new EnemyDefinition("branch-bandit-outlaw-camp", EnemyArchetype.Bandit, new Vector2(2700, 360), 180),
            new EnemyDefinition("branch-snake-camp-exit", EnemyArchetype.SidewinderSnake, new Vector2(3260, 420), 0),
            new EnemyDefinition("branch-armadillo-dry-wash", EnemyArchetype.DynamiteArmadillo, new Vector2(3700, 480), 160),
            new EnemyDefinition("branch-bandit-blasting-perch", EnemyArchetype.Bandit, new Vector2(4680, 240), 80, -1),
            new EnemyDefinition("branch-wildlife-terrace-exit", EnemyArchetype.Wildlife, new Vector2(5250, 400), 80),
            new EnemyDefinition("branch-snake-final-rise", EnemyArchetype.SidewinderSnake, new Vector2(5700, 480), 0),
            new EnemyDefinition("branch-bandit-shortcut-overlook", EnemyArchetype.Bandit, new Vector2(6024, 480), 96, -1)),
        ImmutableArray.Create(
            new PickupDefinition("branch-reserve-ammo", new Vector2(760, 448), PickupType.ReserveAmmo),
            new PickupDefinition("branch-currency-vulture-crown", new Vector2(1770, 208), PickupType.Currency),
            new PickupDefinition("branch-health-oasis", new Vector2(2784, 328), PickupType.Health),
            new PickupDefinition("branch-currency-outlaw-payroll", new Vector2(3050, 328), PickupType.Currency),
            new PickupDefinition("branch-reserve-ammo-dynamite", new Vector2(4680, 208), PickupType.ReserveAmmo),
            new PickupDefinition("branch-health-gallows", new Vector2(5500, 448), PickupType.Health),
            new PickupDefinition("branch-currency-shortcut", new Vector2(6024, 448), PickupType.Currency)));

    public static RoomDefinition ForId(int id) => id == Branch.Id ? Branch : Hub;
}
