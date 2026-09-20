using Cowbania.Core.Gameplay.Input;

namespace Cowbania.Core.Gameplay.Combat;

internal static class RevolverSystem
{
    internal static void Update(GameWorldState state, InputFrame input, float elapsedSeconds)
    {
        if (state.ReloadTimer > 0)
        {
            state.ReloadTimer = MathF.Max(0, state.ReloadTimer - elapsedSeconds);
            if (state.ReloadTimer == 0) state.Ammo = 6;
        }
        if (input.ReloadPressed && state.Ammo < 6 && state.ReloadTimer <= 0)
            BeginReload(state, elapsedSeconds);
        if (input.FireHeld && state.ReloadTimer <= 0 && state.Ammo > 0 && state.FireTimer <= 0)
        {
            state.Ammo--;
            state.FireTimer = GameWorld.FireDelay;
            state.Projectiles.Add(new(
                state.PlayerPosition + GameWorld.PlayerMuzzleOffset +
                state.AimDirection * GameWorld.PlayerMuzzleDistance,
                state.AimDirection * GameWorld.RevolverProjectileSpeed,
                1));
            state.PlayerShotAcceptedThisUpdate = true;
            if (state.Ammo == 0) BeginReload(state, elapsedSeconds);
        }
    }

    private static void BeginReload(GameWorldState state, float elapsedSeconds)
    {
        state.ReloadTimer = MathF.Max(0, GameWorld.ReloadDuration - elapsedSeconds);
        if (state.ReloadTimer == 0)
            state.Ammo = 6;
    }
}
