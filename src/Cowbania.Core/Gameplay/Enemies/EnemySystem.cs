using System.Numerics;
using Cowbania.Core.Gameplay.Combat;
using Cowbania.Core.Gameplay.World;

namespace Cowbania.Core.Gameplay.Enemies;

internal static class EnemySystem
{
    internal static bool Update(GameWorldState state, float dt)
    {
        var roomId = state.Room;
        var enemies = state.CurrentEnemies;
        foreach (var enemy in enemies)
        {
            if (!enemy.Alive)
                continue;

            var distance = Vector2.Distance(state.PlayerPosition, enemy.Position);
            switch (enemy.BehaviorState)
            {
                case EnemyBehaviorState.Patrol:
                    if (distance <= GameWorld.EnemyDisengageRadius)
                        enemy.EnterState(EnemyBehaviorState.Notice);
                    else
                        MoveEnemy(state, enemy, enemy.FacingDirection * GameWorld.EnemyPatrolSpeed, dt);
                    break;

                case EnemyBehaviorState.Notice:
                    if (distance > GameWorld.EnemyDisengageRadius)
                    {
                        enemy.EnterState(EnemyBehaviorState.Patrol);
                        break;
                    }
                    FacePlayer(state, enemy);
                    enemy.StateElapsed += dt;
                    if (enemy.StateElapsed >= GameWorld.EnemyNoticeDuration)
                        enemy.EnterState(EnemyBehaviorState.Chase);
                    break;

                case EnemyBehaviorState.Chase:
                    if (distance > GameWorld.EnemyDisengageRadius)
                    {
                        enemy.EnterState(EnemyBehaviorState.Patrol);
                        break;
                    }

                    FacePlayer(state, enemy);
                    if (enemy.Definition.Archetype == EnemyArchetype.Bandit)
                    {
                        var horizontalDistance = MathF.Abs(state.PlayerPosition.X - enemy.Position.X);
                        if (horizontalDistance >= GameWorld.BanditMinimumAttackRange &&
                            horizontalDistance <= GameWorld.BanditMaximumAttackRange)
                        {
                            BeginAttack(state, enemy);
                        }
                        else
                        {
                            var direction = horizontalDistance < GameWorld.BanditMinimumAttackRange
                                ? -enemy.FacingDirection
                                : enemy.FacingDirection;
                            MoveEnemy(state, enemy, direction * GameWorld.EnemyChaseSpeed, dt);
                        }
                    }
                    else if (distance <= GameWorld.WildlifeAttackRange)
                    {
                        BeginAttack(state, enemy);
                    }
                    else
                    {
                        MoveEnemy(state, enemy, enemy.FacingDirection * GameWorld.EnemyChaseSpeed, dt);
                    }
                    break;

                case EnemyBehaviorState.Attack:
                    if (distance > GameWorld.EnemyDisengageRadius)
                    {
                        enemy.EnterState(EnemyBehaviorState.Patrol);
                        break;
                    }
                    if (UpdateAttack(state, enemy, dt))
                        return true;
                    if (state.Room != roomId)
                        return true;
                    break;
            }
        }

        return false;
    }

    internal static bool TryDamagePlayer(GameWorldState state, int damage)
    {
        if (state.InvulnerabilityTimer > 0)
            return false;

        state.Health -= damage;
        state.InvulnerabilityTimer = 0.5f;
        if (state.Health > 0)
            return false;

        WorldProgressionSystem.Respawn(state);
        return true;
    }

    private static void BeginAttack(GameWorldState state, EnemyRuntime enemy)
    {
        enemy.EnterState(EnemyBehaviorState.Attack);
        enemy.AttackPhase = EnemyAttackPhase.Telegraph;
        enemy.AttackElapsed = 0;
        enemy.DamageAppliedThisAttack = false;
        FacePlayer(state, enemy);
    }

    private static bool UpdateAttack(GameWorldState state, EnemyRuntime enemy, float dt)
    {
        FacePlayer(state, enemy);
        enemy.StateElapsed += dt;
        enemy.AttackElapsed += dt;

        if (enemy.AttackPhase == EnemyAttackPhase.Telegraph)
        {
            var duration = enemy.Definition.Archetype == EnemyArchetype.Bandit
                ? GameWorld.BanditTelegraphDuration
                : GameWorld.WildlifeTelegraphDuration;
            if (enemy.AttackElapsed < duration)
                return false;

            enemy.AttackPhase = EnemyAttackPhase.Active;
            enemy.AttackElapsed = 0;
            if (enemy.Definition.Archetype == EnemyArchetype.Bandit)
            {
                var velocity = new Vector2(enemy.FacingDirection * GameWorld.BanditProjectileSpeed, 0);
                state.Projectiles.Add(new ProjectileState(
                    enemy.Position + new Vector2(
                        enemy.FacingDirection * GameWorld.PlayerMuzzleDistance,
                        GameWorld.PlayerMuzzleOffset.Y),
                    velocity,
                    1,
                    ProjectileOwner.Enemy,
                    ProjectileKind.BanditBullet,
                    enemy.Definition.Id));
            }
            return false;
        }

        if (enemy.AttackPhase == EnemyAttackPhase.Active)
        {
            var duration = enemy.Definition.Archetype == EnemyArchetype.Bandit
                ? GameWorld.BanditActiveDuration
                : GameWorld.WildlifeLungeDuration;
            if (enemy.Definition.Archetype == EnemyArchetype.Wildlife)
            {
                MoveEnemy(state, enemy, enemy.FacingDirection * GameWorld.WildlifeLungeSpeed, dt);
                if (!enemy.DamageAppliedThisAttack &&
                    Vector2.Distance(state.PlayerPosition, enemy.Position) < 28)
                {
                    enemy.DamageAppliedThisAttack = true;
                    if (TryDamagePlayer(state, 1))
                        return true;
                }
            }

            if (enemy.AttackElapsed < duration)
                return false;

            enemy.AttackPhase = EnemyAttackPhase.Recovery;
            enemy.AttackElapsed = 0;
            enemy.Velocity = Vector2.Zero;
            return false;
        }

        var recoveryDuration = enemy.Definition.Archetype == EnemyArchetype.Bandit
            ? GameWorld.BanditRecoveryDuration
            : GameWorld.WildlifeRecoveryDuration;
        if (enemy.AttackElapsed >= recoveryDuration)
            enemy.EnterState(
                Vector2.Distance(state.PlayerPosition, enemy.Position) > GameWorld.EnemyDisengageRadius
                    ? EnemyBehaviorState.Patrol
                    : EnemyBehaviorState.Chase);
        return false;
    }

    private static void MoveEnemy(
        GameWorldState state,
        EnemyRuntime enemy,
        float horizontalVelocity,
        float dt)
    {
        var room = state.CurrentRoom;
        var minimumX = MathF.Max(
            room.Bounds.X,
            enemy.Definition.Spawn.X - enemy.Definition.HorizontalLeash);
        var maximumX = MathF.Min(
            room.Bounds.Right,
            enemy.Definition.Spawn.X + enemy.Definition.HorizontalLeash);
        var previousPosition = enemy.Position;
        var desiredX = enemy.Position.X + horizontalVelocity * dt;
        var clampedX = Math.Clamp(desiredX, minimumX, maximumX);
        enemy.Position = new Vector2(clampedX, enemy.Definition.Spawn.Y);
        enemy.Velocity = new Vector2(dt > 0 ? (clampedX - previousPosition.X) / dt : 0, 0);

        if (horizontalVelocity != 0)
            enemy.FacingDirection = Math.Sign(horizontalVelocity);
        if (desiredX != clampedX)
            enemy.FacingDirection *= -1;
    }

    private static void FacePlayer(GameWorldState state, EnemyRuntime enemy)
    {
        var delta = state.PlayerPosition.X - enemy.Position.X;
        if (MathF.Abs(delta) > 0.001f)
            enemy.FacingDirection = Math.Sign(delta);
        enemy.Velocity = Vector2.Zero;
    }
}
