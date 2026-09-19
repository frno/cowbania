using System.Numerics;

namespace Cowbania.Host.Feedback;

internal readonly record struct FrameFeedbackSnapshot(
    InputFrame Input,
    int PreviousHealth,
    int PreviousPickupCount,
    bool PreviousReloading,
    bool PreviousPaused,
    int PreviousRoom,
    ObjectivePhase PreviousObjective,
    int PreviousCheckpointRoom,
    Vector2 PreviousCheckpointPosition,
    IReadOnlyList<EnemyState> PreviousEnemies,
    IReadOnlyList<ProjectileState> PreviousProjectiles)
{
    public bool PlayerDiedOrChangedRoom(GameWorld world) =>
        PreviousRoom != world.Room ||
        PreviousHealth == 1 && world.Health == GameWorld.MaximumHealth;

    public bool SimulationActive(GameWorld world) => !world.IsPaused && !world.Completed;
}
