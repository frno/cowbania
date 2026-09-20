using System.Numerics;

namespace Cowbania.Core.Gameplay.Enemies;

public enum EnemyArchetype
{
    Bandit,
    Wildlife,
    DynamiteArmadillo,
    SidewinderSnake
}

public enum EnemyBehaviorState
{
    Hidden,
    Patrol,
    Notice,
    Chase,
    Attack,
    Defeated
}

public enum EnemyAttackPhase
{
    None,
    Telegraph,
    Active,
    Recovery
}

public readonly record struct EnemyDefinition(
    string Id,
    EnemyArchetype Archetype,
    Vector2 Spawn,
    float HorizontalLeash,
    int InitialFacingDirection = 1);

public readonly record struct EnemyState(
    string Id,
    EnemyArchetype Archetype,
    EnemyBehaviorState BehaviorState,
    EnemyAttackPhase AttackPhase,
    Vector2 Position,
    Vector2 Velocity,
    int FacingDirection,
    int Health,
    bool Alive,
    float StateTimerNormalized,
    float AttackTimerNormalized)
{
    public EnemyState(Vector2 position, int health, bool alive)
        : this(
            string.Empty,
            EnemyArchetype.Bandit,
            alive ? EnemyBehaviorState.Patrol : EnemyBehaviorState.Defeated,
            EnemyAttackPhase.None,
            position,
            Vector2.Zero,
            1,
            health,
            alive,
            alive ? 0f : 1f,
            0f)
    {
    }
}
