using Cowbania.Host.Diagnostics;
using Cowbania.Host.Feedback;

namespace Cowbania.Host.Audio;

internal sealed class AudioFeedbackRouter(AudioEventBus audioBus)
{
    public void Route(FrameFeedbackSnapshot signals, GameWorld world, long frame)
    {
        if (world.LastJumpRequestOutcome == JumpRequestOutcome.Accepted)
        {
            RuntimeLog.Info($"jump audio dispatch frame={frame} room={world.Room}");
            audioBus.Play(AudioEvent.Jump);
        }
        if (signals.Input.DashPressed && world.IsDashing) audioBus.Play(AudioEvent.Dash);
        if (!signals.PreviousReloading && world.IsReloading) audioBus.Play(AudioEvent.Reload);
        if (world.PlayerShotAcceptedThisUpdate) audioBus.Play(AudioEvent.Shooting);
        if (world.Health < signals.PreviousHealth) audioBus.Play(AudioEvent.Damage);
        if (world.CollectedPickupCount > signals.PreviousPickupCount) audioBus.Play(AudioEvent.Pickup);
        if (StartedAttack(signals.PreviousEnemies, world.Enemies, EnemyArchetype.Bandit))
            audioBus.Play(AudioEvent.Shooting);
        if (StartedAttack(signals.PreviousEnemies, world.Enemies, EnemyArchetype.Wildlife))
            audioBus.Play(AudioEvent.Dash);
    }

    private static bool StartedAttack(
        IReadOnlyList<EnemyState> previousEnemies,
        IReadOnlyList<EnemyState> currentEnemies,
        EnemyArchetype archetype)
    {
        var previousById = previousEnemies.ToDictionary(enemy => enemy.Id, StringComparer.Ordinal);
        return currentEnemies.Any(enemy =>
            enemy.Archetype == archetype &&
            enemy.AttackPhase == EnemyAttackPhase.Active &&
            (!previousById.TryGetValue(enemy.Id, out var previous) ||
             previous.AttackPhase != EnemyAttackPhase.Active));
    }
}
