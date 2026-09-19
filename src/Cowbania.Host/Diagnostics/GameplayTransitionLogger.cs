using Cowbania.Host.Feedback;

namespace Cowbania.Host.Diagnostics;

internal sealed class GameplayTransitionLogger
{
    public void LogJumpRequest(long frame, GameWorld world) =>
        RuntimeLog.Info(
            $"jump request frame={frame} room={world.Room} position=({world.PlayerPosition.X:F1},{world.PlayerPosition.Y:F1}) " +
            $"velocity=({world.PlayerVelocity.X:F1},{world.PlayerVelocity.Y:F1}) grounded={world.IsGrounded} " +
            $"dashing={world.IsDashing} paused={world.IsPaused} completed={world.Completed}");

    public void LogJumpOutcome(long frame, GameWorld world) =>
        RuntimeLog.Info(
            $"jump {world.LastJumpRequestOutcome} frame={frame} room={world.Room} position=({world.PlayerPosition.X:F1},{world.PlayerPosition.Y:F1}) " +
            $"velocity=({world.PlayerVelocity.X:F1},{world.PlayerVelocity.Y:F1}) grounded={world.IsGrounded} " +
            $"dashing={world.IsDashing} paused={world.IsPaused} completed={world.Completed}");

    public void Log(long frame, FrameFeedbackSnapshot previous, GameWorld world)
    {
        if (previous.PreviousPaused != world.IsPaused)
            RuntimeLog.Info($"state pause {previous.PreviousPaused}->{world.IsPaused} frame={frame}");
        if (previous.PreviousRoom != world.Room)
            RuntimeLog.Info($"state room {previous.PreviousRoom}->{world.Room} frame={frame} position=({world.PlayerPosition.X:F1},{world.PlayerPosition.Y:F1})");
        if (previous.PreviousObjective != world.ObjectivePhase)
            RuntimeLog.Info($"state objective {previous.PreviousObjective}->{world.ObjectivePhase} frame={frame}");
        if (previous.PreviousCheckpointRoom != world.CheckpointRoom ||
            previous.PreviousCheckpointPosition != world.CheckpointPosition)
            RuntimeLog.Info(
                $"state checkpoint room={previous.PreviousCheckpointRoom}->{world.CheckpointRoom} " +
                $"position=({previous.PreviousCheckpointPosition.X:F1},{previous.PreviousCheckpointPosition.Y:F1})->" +
                $"({world.CheckpointPosition.X:F1},{world.CheckpointPosition.Y:F1})");
        if (previous.PreviousHealth == 1 && world.Health == GameWorld.MaximumHealth)
            RuntimeLog.Warn($"state death/respawn frame={frame} room={world.Room} position=({world.PlayerPosition.X:F1},{world.PlayerPosition.Y:F1})");
        else if (previous.PreviousHealth != world.Health)
            RuntimeLog.Info($"state health {previous.PreviousHealth}->{world.Health} frame={frame}");
        LogEncounters(frame, previous.PreviousEnemies, previous.PreviousProjectiles, world);
    }

    private static void LogEncounters(
        long frame,
        IReadOnlyList<EnemyState> previousEnemies,
        IReadOnlyList<ProjectileState> previousProjectiles,
        GameWorld world)
    {
        var previousById = previousEnemies.ToDictionary(enemy => enemy.Id, StringComparer.Ordinal);
        foreach (var enemy in world.Enemies)
        {
            if (!previousById.TryGetValue(enemy.Id, out var previous))
            {
                RuntimeLog.Info($"enemy snapshot added id=\"{enemy.Id}\" archetype={enemy.Archetype} state={enemy.BehaviorState} phase={enemy.AttackPhase} frame={frame}");
                continue;
            }
            if (previous.BehaviorState != enemy.BehaviorState ||
                previous.AttackPhase != enemy.AttackPhase ||
                previous.Alive != enemy.Alive)
                RuntimeLog.Info(
                    $"enemy transition id=\"{enemy.Id}\" archetype={enemy.Archetype} " +
                    $"state={previous.BehaviorState}->{enemy.BehaviorState} phase={previous.AttackPhase}->{enemy.AttackPhase} " +
                    $"alive={previous.Alive}->{enemy.Alive} frame={frame}");
        }
        var previousCount = previousProjectiles.Count(projectile => projectile.Owner == ProjectileOwner.Enemy);
        var hostile = world.Projectiles.Where(projectile => projectile.Owner == ProjectileOwner.Enemy).ToArray();
        if (hostile.Length > previousCount)
            RuntimeLog.Info(
                $"hostile projectiles count={previousCount}->{hostile.Length} " +
                $"sources=\"{string.Join(",", hostile.Select(projectile => projectile.SourceId).Distinct())}\" frame={frame}");
    }
}
