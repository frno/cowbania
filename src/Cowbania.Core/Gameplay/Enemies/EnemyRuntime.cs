using System.Numerics;

namespace Cowbania.Core.Gameplay.Enemies;

internal sealed class EnemyRuntime
{
    internal EnemyRuntime(EnemyDefinition definition)
    {
        Definition = definition;
        Reset();
    }

    internal EnemyDefinition Definition { get; }
    internal EnemyBehaviorState BehaviorState { get; set; }
    internal EnemyAttackPhase AttackPhase { get; set; }
    internal Vector2 Position { get; set; }
    internal Vector2 Velocity { get; set; }
    internal int FacingDirection { get; set; }
    internal int Health { get; set; }
    internal bool Alive { get; set; }
    internal float StateElapsed { get; set; }
    internal float AttackElapsed { get; set; }
    internal bool DamageAppliedThisAttack { get; set; }
    internal bool TriggerReady { get; set; }

    internal EnemyState Snapshot => new(
        Definition.Id,
        Definition.Archetype,
        BehaviorState,
        AttackPhase,
        Position,
        Velocity,
        FacingDirection,
        Health,
        Alive,
        StateProgress,
        AttackProgress);

    private float StateProgress => BehaviorState switch
    {
        EnemyBehaviorState.Notice => Normalize(StateElapsed, GameWorld.EnemyNoticeDuration),
        EnemyBehaviorState.Attack => AttackProgress,
        EnemyBehaviorState.Defeated => 1f,
        _ => 0f
    };

    private float AttackProgress => AttackPhase switch
    {
        EnemyAttackPhase.Telegraph => Normalize(
            AttackElapsed, GetAttackPhaseDuration(EnemyAttackPhase.Telegraph)),
        EnemyAttackPhase.Active => Normalize(
            AttackElapsed, GetAttackPhaseDuration(EnemyAttackPhase.Active)),
        EnemyAttackPhase.Recovery => Normalize(
            AttackElapsed, GetAttackPhaseDuration(EnemyAttackPhase.Recovery)),
        _ => 0f
    };

    internal void EnterState(EnemyBehaviorState state)
    {
        BehaviorState = state;
        StateElapsed = 0;
        Velocity = Vector2.Zero;
        if (state != EnemyBehaviorState.Attack)
        {
            AttackPhase = EnemyAttackPhase.None;
            AttackElapsed = 0;
            DamageAppliedThisAttack = false;
        }
    }

    internal void Damage(int damage)
    {
        if (!Alive || damage <= 0 || Definition.Archetype == EnemyArchetype.DynamiteArmadillo)
            return;

        Health -= damage;
        if (Health > 0)
            return;

        Health = 0;
        Alive = false;
        EnterState(EnemyBehaviorState.Defeated);
    }

    internal void ApplySnapshot(EnemyState state)
    {
        Position = state.Position;
        Velocity = state.Velocity;
        FacingDirection = state.FacingDirection is -1 or 1
            ? state.FacingDirection
            : Definition.InitialFacingDirection;
        Health = state.Health;
        Alive = state.Alive;
        BehaviorState = state.Alive ? state.BehaviorState : EnemyBehaviorState.Defeated;
        AttackPhase = state.Alive ? state.AttackPhase : EnemyAttackPhase.None;
        StateElapsed = 0;
        AttackElapsed = 0;
        DamageAppliedThisAttack = false;
        TriggerReady = BehaviorState == EnemyBehaviorState.Hidden;
    }

    private void Reset()
    {
        Position = Definition.Spawn;
        Velocity = Vector2.Zero;
        FacingDirection = Definition.InitialFacingDirection is -1 or 1
            ? Definition.InitialFacingDirection
            : 1;
        Health = Definition.Archetype == EnemyArchetype.SidewinderSnake
            ? GameWorld.SidewinderSnakeHealth
            : GameWorld.EnemyMaximumHealth;
        Alive = true;
        BehaviorState = Definition.Archetype == EnemyArchetype.SidewinderSnake
            ? EnemyBehaviorState.Hidden
            : EnemyBehaviorState.Patrol;
        AttackPhase = EnemyAttackPhase.None;
        StateElapsed = 0;
        AttackElapsed = 0;
        DamageAppliedThisAttack = false;
        TriggerReady = true;
    }

    private static float Normalize(float elapsed, float duration) =>
        duration <= 0 ? 1f : Math.Clamp(elapsed / duration, 0f, 1f);

    private float GetAttackPhaseDuration(EnemyAttackPhase phase) => (Definition.Archetype, phase) switch
    {
        (EnemyArchetype.Bandit, EnemyAttackPhase.Telegraph) => GameWorld.BanditTelegraphDuration,
        (EnemyArchetype.Bandit, EnemyAttackPhase.Active) => GameWorld.BanditActiveDuration,
        (EnemyArchetype.Bandit, EnemyAttackPhase.Recovery) => GameWorld.BanditRecoveryDuration,
        (EnemyArchetype.Wildlife, EnemyAttackPhase.Telegraph) => GameWorld.WildlifeTelegraphDuration,
        (EnemyArchetype.Wildlife, EnemyAttackPhase.Active) => GameWorld.WildlifeLungeDuration,
        (EnemyArchetype.Wildlife, EnemyAttackPhase.Recovery) => GameWorld.WildlifeRecoveryDuration,
        (EnemyArchetype.DynamiteArmadillo, EnemyAttackPhase.Active) => GameWorld.DynamiteArmadilloRollDuration,
        (EnemyArchetype.DynamiteArmadillo, EnemyAttackPhase.Recovery) => GameWorld.DynamiteArmadilloRecoveryDuration,
        (EnemyArchetype.SidewinderSnake, EnemyAttackPhase.Telegraph) => GameWorld.SidewinderSnakeRisingDuration,
        (EnemyArchetype.SidewinderSnake, EnemyAttackPhase.Active) => GameWorld.SidewinderSnakeExposedDuration,
        (EnemyArchetype.SidewinderSnake, EnemyAttackPhase.Recovery) => GameWorld.SidewinderSnakeRetreatDuration,
        _ => 0f
    };
}
