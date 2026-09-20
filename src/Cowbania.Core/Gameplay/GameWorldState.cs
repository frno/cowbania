using System.Numerics;
using Cowbania.Core.Gameplay.Combat;
using Cowbania.Core.Gameplay.Enemies;
using Cowbania.Core.Gameplay.Player;
using Cowbania.Core.Gameplay.World;

namespace Cowbania.Core.Gameplay;

internal sealed class GameWorldState
{
    internal readonly List<ProjectileState> Projectiles = new();
    internal readonly Dictionary<int, List<EnemyRuntime>> EnemiesByRoom = new();
    internal readonly HashSet<string> CollectedPickupIds = new(StringComparer.Ordinal);

    internal float DashTimer;
    internal float DashCooldown;
    internal float FireTimer;
    internal float ReloadTimer;
    internal float InvulnerabilityTimer;
    internal bool CheckpointActivated;

    internal Vector2 PlayerPosition = RoomCatalog.Hub.Spawn;
    internal Vector2 PlayerVelocity;
    internal Vector2 CheckpointPosition = RoomCatalog.Hub.Checkpoint;
    internal int CheckpointRoom = RoomCatalog.Hub.Id;
    internal int Health = GameWorld.MaximumHealth;
    internal int Ammo = 6;
    internal int ReserveAmmo;
    internal int Score;
    internal int SelectedWeaponSlot = 1;
    internal JumpRequestOutcome LastJumpRequestOutcome;
    internal bool PlayerShotAcceptedThisUpdate;
    internal bool ArmadilloShotBlockedThisUpdate;
    internal bool IsPaused;
    internal bool ShortcutUnlocked;
    internal bool Completed;
    internal int Room;
    internal Vector2 AimDirection = Vector2.UnitX;
    internal int FacingDirection = 1;

    internal RoomDefinition CurrentRoom => RoomCatalog.ForId(Room);

    internal List<EnemyRuntime> CurrentEnemies
    {
        get
        {
            if (!EnemiesByRoom.TryGetValue(Room, out var enemies))
            {
                var room = CurrentRoom;
                enemies = room.EnemyDefinitions.Select(definition => new EnemyRuntime(definition)).ToList();
                EnemiesByRoom[Room] = enemies;
            }

            return enemies;
        }
    }
}
