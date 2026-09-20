namespace Cowbania.Core.Tests.Harness;

internal static class WorldSnapshots
{
    public static WorldSnapshot CaptureWorld(GameWorld game) => new(
        game.PlayerPosition, game.PlayerVelocity, game.CheckpointPosition, game.CheckpointRoom,
        game.Health, game.Ammo, game.ReserveAmmo, game.Score, game.SelectedWeaponSlot,
        game.CollectedPickupCount, game.Room, game.AimDirection, game.FacingDirection,
        game.IsPaused, game.ShortcutUnlocked, game.Completed, game.Projectiles.ToArray(),
        game.Enemies.ToArray(), GetField<float>(game, "dashTimer"),
        GetField<float>(game, "dashCooldown"), GetField<float>(game, "fireTimer"),
        GetField<float>(game, "reloadTimer"), GetField<float>(game, "invulnerabilityTimer"));
}

internal readonly record struct WorldSnapshot(
    Vector2 PlayerPosition,
    Vector2 PlayerVelocity,
    Vector2 CheckpointPosition,
    int CheckpointRoom,
    int Health,
    int Ammo,
    int ReserveAmmo,
    int Score,
    int SelectedWeaponSlot,
    int CollectedPickupCount,
    int Room,
    Vector2 AimDirection,
    int FacingDirection,
    bool IsPaused,
    bool ShortcutUnlocked,
    bool Completed,
    ProjectileState[] Projectiles,
    EnemyState[] Enemies,
    float DashTimer,
    float DashCooldown,
    float FireTimer,
    float ReloadTimer,
    float InvulnerabilityTimer)
{
    public bool Equals(WorldSnapshot other) =>
        PlayerPosition == other.PlayerPosition &&
        PlayerVelocity == other.PlayerVelocity &&
        CheckpointPosition == other.CheckpointPosition &&
        CheckpointRoom == other.CheckpointRoom &&
        Health == other.Health &&
        Ammo == other.Ammo &&
        ReserveAmmo == other.ReserveAmmo &&
        Score == other.Score &&
        SelectedWeaponSlot == other.SelectedWeaponSlot &&
        CollectedPickupCount == other.CollectedPickupCount &&
        Room == other.Room &&
        AimDirection == other.AimDirection &&
        FacingDirection == other.FacingDirection &&
        IsPaused == other.IsPaused &&
        ShortcutUnlocked == other.ShortcutUnlocked &&
        Completed == other.Completed &&
        Projectiles.SequenceEqual(other.Projectiles) &&
        Enemies.SequenceEqual(other.Enemies) &&
        DashTimer == other.DashTimer &&
        DashCooldown == other.DashCooldown &&
        FireTimer == other.FireTimer &&
        ReloadTimer == other.ReloadTimer &&
        InvulnerabilityTimer == other.InvulnerabilityTimer;

    public override int GetHashCode() => HashCode.Combine(
        PlayerPosition, PlayerVelocity, CheckpointPosition, Health, Ammo, Room, AimDirection, FacingDirection);
}
