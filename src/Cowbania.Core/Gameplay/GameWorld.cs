using System.Numerics;
using Cowbania.Core.Gameplay.Combat;
using Cowbania.Core.Gameplay.Enemies;
using Cowbania.Core.Gameplay.Input;
using Cowbania.Core.Gameplay.Pickups;
using Cowbania.Core.Gameplay.Player;
using Cowbania.Core.Gameplay.World;

namespace Cowbania.Core.Gameplay;

public sealed class GameWorld
{
    public const float PlayerBodyWidth = 24f;
    public const float PlayerBodyHeight = 48f;
    public const float PlayerMuzzleDistance = 16f;
    public const float PlayerMuzzleHeight = 30f;
    public const float PlayerSpeed = 360f;
    public const float JumpSpeed = -720f;
    public const float DashSpeed = 720f;
    public const float DashDuration = 0.18f;
    public const float FireDelay = 0.14f;
    public const float ReloadDuration = FireDelay * 4;
    public const float RevolverProjectileSpeed = 720f;
    public const float RevolverProjectileRange = 420f;
    public const float Gravity = 1680f;
    public const float InteractionRadius = 42f;
    public const float PickupRadius = 32f;
    public const float PickupHoverHeight = 20f;
    public const int PointsPerCoin = 100;
    public const int MaximumHealth = 3;
    public const int EnemyMaximumHealth = 2;
    public const float EnemyNoticeDuration = 0.30f;
    public const float EnemyDisengageRadius = 560f;
    public const float BanditMinimumAttackRange = 180f;
    public const float BanditMaximumAttackRange = 520f;
    public const float BanditTelegraphDuration = 0.45f;
    public const float BanditProjectileSpeed = 300f;
    public const float BanditRecoveryDuration = 0.75f;
    public const float WildlifeAttackRange = 150f;
    public const float WildlifeTelegraphDuration = 0.35f;
    public const float WildlifeLungeDuration = 0.22f;
    public const float WildlifeLungeSpeed = 480f;
    public const float WildlifeRecoveryDuration = 0.55f;
    public const float DynamiteArmadilloNoticeHorizontalRange = 160f;
    public const float DynamiteArmadilloNoticeVerticalRange = 32f;
    public const float DynamiteArmadilloRollDuration = 0.55f;
    public const float DynamiteArmadilloRecoveryDuration = 0.55f;
    public const float SidewinderSnakeTriggerHorizontalRange = 128f;
    public const float SidewinderSnakeTriggerVerticalRange = 40f;
    public const float SidewinderSnakeRisingDuration = 0.25f;
    public const float SidewinderSnakeExposedDuration = 0.80f;
    public const float SidewinderSnakeRetreatDuration = 0.30f;
    public const int SidewinderSnakeHealth = 1;
    internal const float PlayerHalfWidth = PlayerBodyWidth / 2f;
    internal const float EnemyPatrolSpeed = 45f;
    internal const float EnemyChaseSpeed = 90f;
    internal const float BanditActiveDuration = 0.05f;
    internal const float DynamiteArmadilloRollSpeed = 420f;
    public static readonly Vector2 PlayerMuzzleOffset = new(0, -24);

    private readonly GameWorldState state = new();

    public Vector2 PlayerPosition
    {
        get => state.PlayerPosition;
        private set => state.PlayerPosition = value;
    }
    public Vector2 PlayerVelocity
    {
        get => state.PlayerVelocity;
        private set => state.PlayerVelocity = value;
    }
    public Vector2 CheckpointPosition
    {
        get => state.CheckpointPosition;
        private set => state.CheckpointPosition = value;
    }
    public int CheckpointRoom
    {
        get => state.CheckpointRoom;
        private set => state.CheckpointRoom = value;
    }
    public int Health
    {
        get => state.Health;
        private set => state.Health = value;
    }
    public int Ammo
    {
        get => state.Ammo;
        private set => state.Ammo = value;
    }
    public int ReserveAmmo
    {
        get => state.ReserveAmmo;
        private set => state.ReserveAmmo = value;
    }
    public int Score
    {
        get => state.Score;
        private set => state.Score = value;
    }
    public int CoinsCollected => state.CollectedPickupIds.Count(id =>
        RoomCatalog.AllPickupsById.TryGetValue(id, out var pickup) && pickup.Type == PickupType.Coin);
    public int TotalCoins => RoomCatalog.TotalCoins;
    public int CurrentRoomCoinsCollected => state.CurrentRoom.Pickups.Count(pickup =>
        pickup.Type == PickupType.Coin && state.CollectedPickupIds.Contains(pickup.Id));
    public int CurrentRoomTotalCoins => state.CurrentRoom.Pickups.Count(pickup => pickup.Type == PickupType.Coin);
    public int SelectedWeaponSlot
    {
        get => state.SelectedWeaponSlot;
        private set => state.SelectedWeaponSlot = value;
    }
    public int CollectedPickupCount => state.CollectedPickupIds.Count;
    public JumpRequestOutcome LastJumpRequestOutcome
    {
        get => state.LastJumpRequestOutcome;
        private set => state.LastJumpRequestOutcome = value;
    }
    public bool PlayerShotAcceptedThisUpdate
    {
        get => state.PlayerShotAcceptedThisUpdate;
        private set => state.PlayerShotAcceptedThisUpdate = value;
    }
    public bool ArmadilloShotBlockedThisUpdate
    {
        get => state.ArmadilloShotBlockedThisUpdate;
        private set => state.ArmadilloShotBlockedThisUpdate = value;
    }
    public bool IsReloading => state.ReloadTimer > 0;
    public bool IsDashing => state.DashTimer > 0;
    public bool IsGrounded => PlayerSystem.IsGrounded(state);
    public bool IsPaused
    {
        get => state.IsPaused;
        private set => state.IsPaused = value;
    }
    public bool ShortcutUnlocked
    {
        get => state.ShortcutUnlocked;
        private set => state.ShortcutUnlocked = value;
    }
    public bool Completed
    {
        get => state.Completed;
        private set => state.Completed = value;
    }
    public ObjectivePhase ObjectivePhase => state.Completed
        ? ObjectivePhase.Completed
        : state.ShortcutUnlocked ? ObjectivePhase.ReturnToHubShortcut : ObjectivePhase.ReachBranchShortcut;
    public int Room
    {
        get => state.Room;
        private set => state.Room = value;
    }
    public RoomDefinition CurrentRoom => state.CurrentRoom;
    public Vector2 AimDirection
    {
        get => state.AimDirection;
        private set => state.AimDirection = value;
    }
    public int FacingDirection
    {
        get => state.FacingDirection;
        private set => state.FacingDirection = value;
    }
    public IReadOnlyList<EnemyState> Enemies => state.CurrentEnemies.Select(enemy => enemy.Snapshot).ToArray();
    public EnemyState Enemy
    {
        get => state.CurrentEnemies[0].Snapshot;
        private set => state.CurrentEnemies[0].ApplySnapshot(value);
    }
    public IReadOnlyList<ProjectileState> Projectiles => state.Projectiles;
    public IEnumerable<PickupDefinition> AvailablePickups =>
        state.CurrentRoom.Pickups.Where(pickup => !state.CollectedPickupIds.Contains(pickup.Id));

    public void Update(InputFrame input, float elapsedSeconds)
    {
        state.PlayerShotAcceptedThisUpdate = false;
        state.ArmadilloShotBlockedThisUpdate = false;
        state.LastJumpRequestOutcome = JumpRequestOutcome.None;
        if (!state.Completed && input.PausePressed) state.IsPaused = !state.IsPaused;
        if (state.IsPaused)
        {
            if (input.JumpPressed) state.LastJumpRequestOutcome = JumpRequestOutcome.RejectedPaused;
            return;
        }
        if (state.Completed)
        {
            if (input.JumpPressed) state.LastJumpRequestOutcome = JumpRequestOutcome.RejectedCompleted;
            return;
        }
        if (input.SelectSlot1Pressed) state.SelectedWeaponSlot = 1;
        PlayerSystem.UpdateInput(state, input, elapsedSeconds);
        RevolverSystem.Update(state, input, elapsedSeconds);
        PlayerSystem.Move(state, input.Horizontal, elapsedSeconds);
        if (state.PlayerPosition.Y > state.CurrentRoom.Bounds.Bottom + PlayerBodyHeight)
        {
            WorldProgressionSystem.RecoverFromFall(state);
            return;
        }
        if (WorldProgressionSystem.TryEnterBranch(state))
            return;

        if (ProjectileSystem.Update(state, elapsedSeconds))
            return;
        if (EnemySystem.Update(state, elapsedSeconds))
            return;
        PickupSystem.Collect(state);
        if (input.InteractPressed) WorldProgressionSystem.Interact(state);
    }
}
