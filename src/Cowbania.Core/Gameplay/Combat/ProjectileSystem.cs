using System.Numerics;
using Cowbania.Core.Gameplay.Enemies;
using Cowbania.Core.Gameplay.World.Geometry;

namespace Cowbania.Core.Gameplay.Combat;

internal static class ProjectileSystem
{
    internal static bool Update(GameWorldState state, float dt)
    {
        for (var i = state.Projectiles.Count - 1; i >= 0; i--)
        {
            var projectile = state.Projectiles[i] with
            {
                Position = state.Projectiles[i].Position + state.Projectiles[i].Velocity * dt
            };
            if (projectile.Owner == ProjectileOwner.Player)
            {
                var consumed = false;
                foreach (var enemy in state.CurrentEnemies)
                {
                    if (!enemy.Alive ||
                        Vector2.Distance(projectile.Position, enemy.Position) >= 30)
                    {
                        continue;
                    }

                    if (enemy.Definition.Archetype == EnemyArchetype.DynamiteArmadillo)
                    {
                        state.ArmadilloShotBlockedThisUpdate = true;
                        state.Projectiles.RemoveAt(i);
                        consumed = true;
                        break;
                    }

                    if (!CanBeHitByPlayerProjectile(enemy))
                        continue;

                    enemy.Damage(projectile.Damage);
                    state.Projectiles.RemoveAt(i);
                    consumed = true;
                    break;
                }

                if (consumed)
                    continue;
            }
            else if (Vector2.Distance(
                         projectile.Position,
                         state.PlayerPosition + new Vector2(0, -GameWorld.PlayerBodyHeight / 2f)) <
                     GameWorld.PlayerBodyHeight / 2f)
            {
                state.Projectiles.RemoveAt(i);
                if (EnemySystem.TryDamagePlayer(state, projectile.Damage))
                    return true;
                continue;
            }

            if (IsOutsideRoom(state, projectile.Position) ||
                state.CurrentRoom.Solids.Any(solid => CollisionQueries.Contains(solid, projectile.Position)))
                state.Projectiles.RemoveAt(i);
            else
                state.Projectiles[i] = projectile;
        }

        return false;
    }

    private static bool IsOutsideRoom(GameWorldState state, Vector2 position) =>
        position.X < state.CurrentRoom.Bounds.X ||
        position.X > state.CurrentRoom.Bounds.Right ||
        position.Y < state.CurrentRoom.Bounds.Y ||
        position.Y > state.CurrentRoom.Bounds.Bottom;

    private static bool CanBeHitByPlayerProjectile(EnemyRuntime enemy) => enemy.Definition.Archetype switch
    {
        EnemyArchetype.DynamiteArmadillo => false,
        EnemyArchetype.SidewinderSnake => enemy.BehaviorState == EnemyBehaviorState.Attack &&
                                          enemy.AttackPhase is EnemyAttackPhase.Telegraph or EnemyAttackPhase.Active,
        _ => true
    };
}
