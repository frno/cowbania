using System.Numerics;

namespace Cowbania.Core.Gameplay.Combat;

public enum ProjectileOwner
{
    Player,
    Enemy
}

public enum ProjectileKind
{
    Revolver,
    BanditBullet
}

public readonly record struct ProjectileState(
    Vector2 Position,
    Vector2 Velocity,
    int Damage,
    ProjectileOwner Owner,
    ProjectileKind Kind,
    string SourceId,
    float RemainingRange = float.PositiveInfinity)
{
    public ProjectileState(Vector2 position, Vector2 velocity, int damage)
        : this(
            position,
            velocity,
            damage,
            ProjectileOwner.Player,
            ProjectileKind.Revolver,
            "player",
            GameWorld.RevolverProjectileRange)
    {
    }
}
