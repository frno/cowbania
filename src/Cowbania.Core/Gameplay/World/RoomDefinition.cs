using System.Collections.Immutable;
using System.Numerics;
using Cowbania.Core.Gameplay.Enemies;
using Cowbania.Core.Gameplay.Pickups;
using Cowbania.Core.Gameplay.World.Geometry;

namespace Cowbania.Core.Gameplay.World;

public sealed record RoomDefinition(
    int Id,
    string Name,
    RoomRect Bounds,
    ImmutableArray<RoomRect> Solids,
    Vector2 Spawn,
    Vector2 Checkpoint,
    Vector2 Shortcut,
    Vector2? Exit,
    ImmutableArray<EnemyDefinition> EnemyDefinitions,
    ImmutableArray<PickupDefinition> Pickups)
{
    public RoomRect Ground => Solids[0];
    public Vector2 Enemy => EnemyDefinitions[0].Spawn;
    public ImmutableArray<Vector2> EnemySpawns =>
        EnemyDefinitions.Select(definition => definition.Spawn).ToImmutableArray();
    public ImmutableArray<PickupDefinition> PickupDefinitions => Pickups;
}
