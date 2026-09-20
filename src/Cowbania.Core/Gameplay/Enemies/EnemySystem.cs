using System.Numerics;
using Cowbania.Core.Gameplay.Combat;
using Cowbania.Core.Gameplay.World;
using Cowbania.Core.Gameplay.World.Geometry;

namespace Cowbania.Core.Gameplay.Enemies;

internal static class EnemySystem
{
    private const float ContactDamageRadius = 28f;

    internal static bool Update(GameWorldState state, float dt)
    {
        var roomId = state.Room;
        var enemies = state.CurrentEnemies;
        foreach (var enemy in enemies)
        {
            if (!enemy.Alive)
                continue;

            var handled = enemy.Definition.Archetype switch
            {
                EnemyArchetype.Bandit or EnemyArchetype.Wildlife => UpdateChasingEnemy(state, enemy, dt),
                EnemyArchetype.DynamiteArmadillo => UpdateDynamiteArmadillo(state, enemy, dt),
                EnemyArchetype.SidewinderSnake => UpdateSidewinderSnake(state, enemy, dt),
                _ => false
            };

            if (handled || state.Room != roomId)
                return true;
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
        SetAttackPhase(enemy, EnemyAttackPhase.Telegraph);
        enemy.DamageAppliedThisAttack = false;
        FacePlayer(state, enemy);
    }

    private static bool UpdateChasingEnemy(GameWorldState state, EnemyRuntime enemy, float dt)
    {
        var distance = Vector2.Distance(state.PlayerPosition, enemy.Position);
        switch (enemy.BehaviorState)
        {
            case EnemyBehaviorState.Patrol:
                if (distance <= GameWorld.EnemyDisengageRadius)
                    enemy.EnterState(EnemyBehaviorState.Notice);
                else
                    MoveEnemy(state, enemy, enemy.FacingDirection * GameWorld.EnemyPatrolSpeed, dt);
                return false;

            case EnemyBehaviorState.Notice:
                if (distance > GameWorld.EnemyDisengageRadius)
                {
                    enemy.EnterState(EnemyBehaviorState.Patrol);
                    return false;
                }

                FacePlayer(state, enemy);
                enemy.StateElapsed += dt;
                if (enemy.StateElapsed >= GameWorld.EnemyNoticeDuration)
                    enemy.EnterState(EnemyBehaviorState.Chase);
                return false;

            case EnemyBehaviorState.Chase:
                if (distance > GameWorld.EnemyDisengageRadius)
                {
                    enemy.EnterState(EnemyBehaviorState.Patrol);
                    return false;
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

                return false;

            case EnemyBehaviorState.Attack:
                if (distance > GameWorld.EnemyDisengageRadius)
                {
                    enemy.EnterState(EnemyBehaviorState.Patrol);
                    return false;
                }

                return UpdateStandardAttack(state, enemy, dt);
        }

        return false;
    }

    private static bool UpdateStandardAttack(GameWorldState state, EnemyRuntime enemy, float dt)
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

            SetAttackPhase(enemy, EnemyAttackPhase.Active);
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

            SetAttackPhase(enemy, EnemyAttackPhase.Recovery);
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

    private static bool UpdateDynamiteArmadillo(GameWorldState state, EnemyRuntime enemy, float dt)
    {
        switch (enemy.BehaviorState)
        {
            case EnemyBehaviorState.Patrol:
                if (IsPlayerWithinArmadilloNoticeRange(state, enemy))
                {
                    enemy.EnterState(EnemyBehaviorState.Notice);
                    FacePlayer(state, enemy);
                }
                else
                {
                    MoveEnemy(state, enemy, enemy.FacingDirection * GameWorld.EnemyPatrolSpeed, dt);
                }

                return false;

            case EnemyBehaviorState.Notice:
                FacePlayer(state, enemy);
                enemy.StateElapsed += dt;
                if (enemy.StateElapsed >= GameWorld.EnemyNoticeDuration)
                {
                    enemy.EnterState(EnemyBehaviorState.Attack);
                    SetAttackPhase(enemy, EnemyAttackPhase.Active);
                }

                return false;

            case EnemyBehaviorState.Attack:
                enemy.StateElapsed += dt;
                enemy.AttackElapsed += dt;
                if (enemy.AttackPhase == EnemyAttackPhase.Active)
                {
                    var blocked = MoveEnemy(state, enemy, enemy.FacingDirection * GameWorld.DynamiteArmadilloRollSpeed, dt, true);
                    if (IsPlayerTouchingEnemy(state, enemy) &&
                        TryDamagePlayer(state, 1))
                    {
                        return true;
                    }

                    if (blocked || enemy.AttackElapsed >= GameWorld.DynamiteArmadilloRollDuration)
                        EnterRecovery(enemy);
                }
                else if (enemy.AttackElapsed >= GameWorld.DynamiteArmadilloRecoveryDuration)
                {
                    enemy.EnterState(EnemyBehaviorState.Patrol);
                }

                return false;
        }

        return false;
    }

    private static bool UpdateSidewinderSnake(GameWorldState state, EnemyRuntime enemy, float dt)
    {
        switch (enemy.BehaviorState)
        {
            case EnemyBehaviorState.Hidden:
                if (!IsPlayerWithinSnakeTriggerRange(state, enemy))
                {
                    enemy.TriggerReady = true;
                    return false;
                }

                if (!enemy.TriggerReady)
                    return false;

                enemy.TriggerReady = false;
                enemy.EnterState(EnemyBehaviorState.Attack);
                SetAttackPhase(enemy, EnemyAttackPhase.Telegraph);
                return false;

            case EnemyBehaviorState.Attack:
                enemy.StateElapsed += dt;
                enemy.AttackElapsed += dt;
                if (enemy.AttackPhase == EnemyAttackPhase.Telegraph)
                {
                    if (enemy.AttackElapsed >= GameWorld.SidewinderSnakeRisingDuration)
                        SetAttackPhase(enemy, EnemyAttackPhase.Active);
                    return false;
                }

                if (enemy.AttackPhase == EnemyAttackPhase.Active)
                {
                    if (IsPlayerTouchingEnemy(state, enemy) &&
                        TryDamagePlayer(state, 1))
                    {
                        return true;
                    }

                    if (enemy.AttackElapsed >= GameWorld.SidewinderSnakeExposedDuration)
                        SetAttackPhase(enemy, EnemyAttackPhase.Recovery);
                    return false;
                }

                if (enemy.AttackElapsed >= GameWorld.SidewinderSnakeRetreatDuration)
                    enemy.EnterState(EnemyBehaviorState.Hidden);
                return false;
        }

        return false;
    }

    private static void MoveEnemy(
        GameWorldState state,
        EnemyRuntime enemy,
        float horizontalVelocity,
        float dt) =>
        MoveEnemy(state, enemy, horizontalVelocity, dt, false);

    private static bool MoveEnemy(
        GameWorldState state,
        EnemyRuntime enemy,
        float horizontalVelocity,
        float dt,
        bool stopAtSolid)
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
        var blockedByBoundary = desiredX != clampedX;
        if (stopAtSolid && horizontalVelocity != 0 &&
            TryClampAgainstSolid(state, enemy, horizontalVelocity, clampedX, out var solidClampedX))
        {
            clampedX = solidClampedX;
        }

        enemy.Position = new Vector2(clampedX, enemy.Definition.Spawn.Y);
        enemy.Velocity = new Vector2(dt > 0 ? (clampedX - previousPosition.X) / dt : 0, 0);

        if (horizontalVelocity != 0)
            enemy.FacingDirection = Math.Sign(horizontalVelocity);
        if (blockedByBoundary || (stopAtSolid && desiredX != clampedX))
            enemy.FacingDirection *= -1;

        return blockedByBoundary || (stopAtSolid && desiredX != clampedX);
    }

    private static void FacePlayer(GameWorldState state, EnemyRuntime enemy)
    {
        var delta = state.PlayerPosition.X - enemy.Position.X;
        if (MathF.Abs(delta) > 0.001f)
            enemy.FacingDirection = Math.Sign(delta);
        enemy.Velocity = Vector2.Zero;
    }

    private static void SetAttackPhase(EnemyRuntime enemy, EnemyAttackPhase phase)
    {
        enemy.AttackPhase = phase;
        enemy.AttackElapsed = 0;
    }

    private static void EnterRecovery(EnemyRuntime enemy)
    {
        SetAttackPhase(enemy, EnemyAttackPhase.Recovery);
        enemy.Velocity = Vector2.Zero;
    }

    private static bool IsPlayerTouchingEnemy(GameWorldState state, EnemyRuntime enemy) =>
        Vector2.Distance(state.PlayerPosition, enemy.Position) < ContactDamageRadius;

    private static bool IsPlayerWithinArmadilloNoticeRange(GameWorldState state, EnemyRuntime enemy)
    {
        var delta = state.PlayerPosition - enemy.Position;
        return MathF.Abs(delta.X) <= GameWorld.DynamiteArmadilloNoticeHorizontalRange &&
               MathF.Abs(delta.Y) <= GameWorld.DynamiteArmadilloNoticeVerticalRange;
    }

    private static bool IsPlayerWithinSnakeTriggerRange(GameWorldState state, EnemyRuntime enemy)
    {
        var delta = state.PlayerPosition - enemy.Definition.Spawn;
        return MathF.Abs(delta.X) <= GameWorld.SidewinderSnakeTriggerHorizontalRange &&
               MathF.Abs(delta.Y) <= GameWorld.SidewinderSnakeTriggerVerticalRange;
    }

    private static bool TryClampAgainstSolid(
        GameWorldState state,
        EnemyRuntime enemy,
        float horizontalVelocity,
        float desiredX,
        out float clampedX)
    {
        const float enemyHalfWidth = 8f;
        const float enemyHeight = 16f;

        clampedX = desiredX;
        var top = enemy.Definition.Spawn.Y - enemyHeight;
        var bottom = enemy.Definition.Spawn.Y;
        var currentLeft = enemy.Position.X - enemyHalfWidth;
        var currentRight = enemy.Position.X + enemyHalfWidth;

        RoomRect? blockingSolid = null;
        if (horizontalVelocity > 0)
        {
            var desiredRight = desiredX + enemyHalfWidth;
            foreach (var solid in state.CurrentRoom.Solids)
            {
                if (!VerticallyOverlaps(top, bottom, solid) ||
                    solid.X < currentRight ||
                    solid.X > desiredRight)
                {
                    continue;
                }

                if (blockingSolid is null || solid.X < blockingSolid.Value.X)
                    blockingSolid = solid;
            }

            if (blockingSolid is null)
                return false;

            clampedX = blockingSolid.Value.X - enemyHalfWidth;
            return true;
        }

        var desiredLeft = desiredX - enemyHalfWidth;
        foreach (var solid in state.CurrentRoom.Solids)
        {
            if (!VerticallyOverlaps(top, bottom, solid) ||
                solid.Right > currentLeft ||
                solid.Right < desiredLeft)
            {
                continue;
            }

            if (blockingSolid is null || solid.Right > blockingSolid.Value.Right)
                blockingSolid = solid;
        }

        if (blockingSolid is null)
            return false;

        clampedX = blockingSolid.Value.Right + enemyHalfWidth;
        return true;
    }

    private static bool VerticallyOverlaps(float top, float bottom, RoomRect solid) =>
        bottom > solid.Y && top < solid.Bottom;
}
