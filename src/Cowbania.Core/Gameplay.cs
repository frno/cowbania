using System.Collections.Immutable;
using System.Numerics;

namespace Cowbania.Core;

public readonly record struct InputFrame(
    int Horizontal,
    bool JumpPressed,
    bool DashPressed,
    Vector2 Aim,
    bool FireHeld,
    bool ReloadPressed,
    bool InteractPressed,
    bool PausePressed,
    bool SelectSlot1Pressed = false);

public readonly record struct ProjectileState(Vector2 Position, Vector2 Velocity, int Damage);
public readonly record struct EnemyState(Vector2 Position, int Health, bool Alive);
public enum JumpRequestOutcome
{
    None,
    Accepted,
    RejectedPaused,
    RejectedCompleted,
    RejectedNotGrounded,
    RejectedDashing
}
public enum PickupType
{
    Currency,
    Health,
    ReserveAmmo
}

public readonly record struct PickupDefinition(string Id, Vector2 Position, PickupType Type);
public enum ObjectivePhase
{
    ReachBranchShortcut,
    ReturnToHubShortcut,
    Completed
}

public readonly record struct RoomRect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
}

public sealed record RoomDefinition(
    int Id,
    string Name,
    RoomRect Bounds,
    ImmutableArray<RoomRect> Solids,
    Vector2 Spawn,
    Vector2 Checkpoint,
    Vector2 Shortcut,
    ImmutableArray<Vector2> EnemySpawns,
    ImmutableArray<PickupDefinition> Pickups)
{
    public RoomRect Ground => Solids[0];
    public Vector2 Enemy => EnemySpawns[0];
    public ImmutableArray<PickupDefinition> PickupDefinitions => Pickups;
}

public static class RoomCatalog
{
    public static readonly RoomDefinition Hub = new(
        0, "Hub", new RoomRect(0, 0, 1600, 576),
        ImmutableArray.Create(
            new RoomRect(0, 480, 1600, 96),
            new RoomRect(250, 380, 220, 24),
            new RoomRect(650, 320, 240, 24)),
        new Vector2(80, 480), new Vector2(80, 480), new Vector2(1440, 480),
        ImmutableArray.Create(new Vector2(520, 480), new Vector2(1040, 480)),
        ImmutableArray.Create(
            new PickupDefinition("hub-currency", new Vector2(1120, 448), PickupType.Currency),
            new PickupDefinition("hub-health", new Vector2(1280, 448), PickupType.Health)));

    public static readonly RoomDefinition Branch = new(
        1, "Branch", new RoomRect(0, 0, 1600, 576),
        ImmutableArray.Create(
            new RoomRect(0, 480, 1600, 96),
            new RoomRect(180, 370, 240, 24),
            new RoomRect(760, 290, 260, 24)),
        new Vector2(40, 480), new Vector2(760, 480), new Vector2(1440, 480),
        ImmutableArray.Create(new Vector2(520, 480), new Vector2(1120, 480)),
        ImmutableArray.Create(
            new PickupDefinition("branch-reserve-ammo", new Vector2(760, 448), PickupType.ReserveAmmo),
            new PickupDefinition("branch-currency", new Vector2(1440, 448), PickupType.Currency)));

    public static RoomDefinition ForId(int id) => id == Branch.Id ? Branch : Hub;
}

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
    public const float ReloadDuration = 1.15f;
    public const float Gravity = 1680f;
    public const float InteractionRadius = 42f;
    public const float PickupRadius = 32f;
    public const int MaximumHealth = 3;
    private const float PlayerHalfWidth = PlayerBodyWidth / 2f;
    public static readonly Vector2 PlayerMuzzleOffset = new(0, -24);

    private readonly List<ProjectileState> projectiles = new();
    private readonly Dictionary<int, List<EnemyState>> enemiesByRoom = new();
    private readonly Dictionary<int, float[]> enemyDirectionsByRoom = new();
    private readonly HashSet<string> collectedPickupIds = new(StringComparer.Ordinal);
    private float dashTimer, dashCooldown, fireTimer, reloadTimer, invulnerabilityTimer;
    private bool checkpointActivated;

    public Vector2 PlayerPosition { get; private set; } = RoomCatalog.Hub.Spawn;
    public Vector2 PlayerVelocity { get; private set; }
    public Vector2 CheckpointPosition { get; private set; } = RoomCatalog.Hub.Checkpoint;
    public int CheckpointRoom { get; private set; } = RoomCatalog.Hub.Id;
    public int Health { get; private set; } = MaximumHealth;
    public int Ammo { get; private set; } = 6;
    public int ReserveAmmo { get; private set; }
    public int Currency { get; private set; }
    public int SelectedWeaponSlot { get; private set; } = 1;
    public int CollectedPickupCount => collectedPickupIds.Count;
    public JumpRequestOutcome LastJumpRequestOutcome { get; private set; }
    public bool IsReloading => reloadTimer > 0;
    public bool IsDashing => dashTimer > 0;
    public bool IsGrounded => CurrentRoom.Solids.Any(s =>
        MathF.Abs(PlayerPosition.Y - s.Y) < 0.01f &&
        PlayerPosition.X + PlayerHalfWidth > s.X && PlayerPosition.X - PlayerHalfWidth < s.Right);
    public bool IsPaused { get; private set; }
    public bool ShortcutUnlocked { get; private set; }
    public bool Completed { get; private set; }
    public ObjectivePhase ObjectivePhase => Completed
        ? ObjectivePhase.Completed
        : ShortcutUnlocked ? ObjectivePhase.ReturnToHubShortcut : ObjectivePhase.ReachBranchShortcut;
    public int Room { get; private set; }
    public RoomDefinition CurrentRoom => RoomCatalog.ForId(Room);
    public Vector2 AimDirection { get; private set; } = Vector2.UnitX;
    public int FacingDirection { get; private set; } = 1;
    public IReadOnlyList<EnemyState> Enemies => CurrentEnemies;
    public EnemyState Enemy
    {
        get => CurrentEnemies[0];
        private set => CurrentEnemies[0] = value;
    }
    public IReadOnlyList<ProjectileState> Projectiles => projectiles;
    public IEnumerable<PickupDefinition> AvailablePickups =>
        CurrentRoom.Pickups.Where(pickup => !collectedPickupIds.Contains(pickup.Id));

    private List<EnemyState> CurrentEnemies
    {
        get
        {
            if (!enemiesByRoom.TryGetValue(Room, out var enemies))
            {
                var room = CurrentRoom;
                enemies = room.EnemySpawns.Select(position => new EnemyState(position, 2, true)).ToList();
                enemiesByRoom[Room] = enemies;
                enemyDirectionsByRoom[Room] = Enumerable.Repeat(1f, enemies.Count).ToArray();
            }

            return enemies;
        }
    }

    public void Update(InputFrame input, float elapsedSeconds)
    {
        LastJumpRequestOutcome = JumpRequestOutcome.None;
        if (!Completed && input.PausePressed) IsPaused = !IsPaused;
        if (IsPaused)
        {
            if (input.JumpPressed) LastJumpRequestOutcome = JumpRequestOutcome.RejectedPaused;
            return;
        }
        if (Completed)
        {
            if (input.JumpPressed) LastJumpRequestOutcome = JumpRequestOutcome.RejectedCompleted;
            return;
        }
        if (input.SelectSlot1Pressed) SelectedWeaponSlot = 1;
        dashCooldown = MathF.Max(0, dashCooldown - elapsedSeconds);
        fireTimer = MathF.Max(0, fireTimer - elapsedSeconds);
        invulnerabilityTimer = MathF.Max(0, invulnerabilityTimer - elapsedSeconds);
        var horizontal = Math.Clamp(input.Horizontal, -1, 1);
        if (horizontal != 0) FacingDirection = horizontal;
        if (input.Aim.LengthSquared() > 0.01f)
        {
            AimDirection = Vector2.Normalize(input.Aim);
            if (MathF.Abs(AimDirection.X) > 0.01f)
                FacingDirection = Math.Sign(AimDirection.X);
        }
        else
        {
            AimDirection = new Vector2(FacingDirection, 0);
        }
        if (input.JumpPressed)
        {
            if (IsGrounded && dashTimer <= 0)
            {
                PlayerVelocity = new(PlayerVelocity.X, JumpSpeed);
                LastJumpRequestOutcome = JumpRequestOutcome.Accepted;
            }
            else
            {
                LastJumpRequestOutcome = dashTimer > 0
                    ? JumpRequestOutcome.RejectedDashing
                    : JumpRequestOutcome.RejectedNotGrounded;
            }
        }
        if (input.DashPressed && dashCooldown <= 0 && dashTimer <= 0) { dashTimer = DashDuration; dashCooldown = 0.35f; }
        if (input.ReloadPressed && Ammo < 6 && reloadTimer <= 0) reloadTimer = ReloadDuration;
        if (reloadTimer > 0) { reloadTimer -= elapsedSeconds; if (reloadTimer <= 0) Ammo = 6; }
        if (input.FireHeld && !IsReloading && Ammo > 0 && fireTimer <= 0)
        {
            Ammo--; fireTimer = FireDelay;
            projectiles.Add(new(PlayerPosition + PlayerMuzzleOffset + AimDirection * PlayerMuzzleDistance, AimDirection * 720, 1));
        }

        var speed = dashTimer > 0 ? DashSpeed : PlayerSpeed;
        var previousFeet = PlayerPosition.Y;
        PlayerVelocity = new(horizontal * speed, PlayerVelocity.Y + Gravity * elapsedSeconds);
        if (dashTimer > 0) dashTimer -= elapsedSeconds;
        var next = PlayerPosition + PlayerVelocity * elapsedSeconds;
        next.X = Math.Clamp(next.X, CurrentRoom.Bounds.X + PlayerHalfWidth, CurrentRoom.Bounds.Right - PlayerHalfWidth);
        ResolveVerticalLanding(previousFeet, ref next);
        PlayerPosition = next;

        UpdateProjectiles(elapsedSeconds);
        UpdateEnemy(elapsedSeconds);
        CollectPickups();
        if (input.InteractPressed) Interact();
    }

    private void CollectPickups()
    {
        foreach (var pickup in CurrentRoom.Pickups)
        {
            if (collectedPickupIds.Contains(pickup.Id) ||
                Vector2.Distance(PlayerPosition, pickup.Position) > PickupRadius)
                continue;

            if (pickup.Type == PickupType.Health && Health >= MaximumHealth)
                continue;

            switch (pickup.Type)
            {
                case PickupType.Currency:
                    Currency++;
                    break;
                case PickupType.Health:
                    Health = Math.Min(MaximumHealth, Health + 1);
                    break;
                case PickupType.ReserveAmmo:
                    ReserveAmmo++;
                    break;
            }

            collectedPickupIds.Add(pickup.Id);
        }
    }

    private void ResolveVerticalLanding(float previousFeet, ref Vector2 next)
    {
        if (PlayerVelocity.Y < 0) return;
        foreach (var solid in CurrentRoom.Solids)
        {
            var overlaps = next.X + PlayerHalfWidth > solid.X && next.X - PlayerHalfWidth < solid.Right;
            if (overlaps && previousFeet <= solid.Y && next.Y >= solid.Y)
            {
                next.Y = solid.Y;
                PlayerVelocity = new(PlayerVelocity.X, 0);
                break;
            }
        }
    }

    private void UpdateProjectiles(float dt)
    {
        for (var i = projectiles.Count - 1; i >= 0; i--)
        {
            var projectile = projectiles[i] with { Position = projectiles[i].Position + projectiles[i].Velocity * dt };
            var hitEnemy = CurrentEnemies.FindIndex(enemy => enemy.Alive && Vector2.Distance(projectile.Position, enemy.Position) < 30);
            if (hitEnemy >= 0)
            {
                var enemy = CurrentEnemies[hitEnemy] with { Health = CurrentEnemies[hitEnemy].Health - projectile.Damage };
                if (enemy.Health <= 0) enemy = enemy with { Alive = false };
                CurrentEnemies[hitEnemy] = enemy;
                projectiles.RemoveAt(i);
            }
            else if (projectile.Position.X < CurrentRoom.Bounds.X || projectile.Position.X > CurrentRoom.Bounds.Right || projectile.Position.Y < CurrentRoom.Bounds.Y || projectile.Position.Y > CurrentRoom.Bounds.Bottom) projectiles.RemoveAt(i);
            else projectiles[i] = projectile;
        }
    }

    private void UpdateEnemy(float dt)
    {
        var enemies = CurrentEnemies;
        var directions = enemyDirectionsByRoom[Room];
        for (var i = 0; i < enemies.Count; i++)
        {
            if (!enemies[i].Alive) continue;
            var spawn = CurrentRoom.EnemySpawns[i];
            var x = enemies[i].Position.X + directions[i] * 45 * dt;
            if (x < spawn.X - 120 || x > spawn.X + 120)
                directions[i] *= -1;
            enemies[i] = enemies[i] with { Position = new(x, CurrentRoom.Ground.Y) };
            if (Vector2.Distance(PlayerPosition, enemies[i].Position) < 28 && invulnerabilityTimer <= 0)
            {
                Health--; invulnerabilityTimer = 0.5f;
                if (Health <= 0) Respawn();
                break;
            }
        }
    }

    private void Interact()
    {
        var room = CurrentRoom;
        if (Room == RoomCatalog.Branch.Id &&
            Vector2.Distance(PlayerPosition, room.Checkpoint) < InteractionRadius)
        {
            checkpointActivated = true;
            CheckpointRoom = RoomCatalog.Branch.Id;
            CheckpointPosition = room.Checkpoint;
        }

        if (Room == RoomCatalog.Branch.Id &&
            Vector2.Distance(PlayerPosition, room.Shortcut) < InteractionRadius)
        {
            ShortcutUnlocked = true;
            Room = RoomCatalog.Hub.Id;
            PlayerPosition = RoomCatalog.Hub.Shortcut;
            PlayerVelocity = Vector2.Zero;
            checkpointActivated = false;
            CheckpointRoom = RoomCatalog.Hub.Id;
            CheckpointPosition = RoomCatalog.Hub.Checkpoint;
        }
        else if (Room == RoomCatalog.Hub.Id &&
                 ShortcutUnlocked &&
                 Vector2.Distance(PlayerPosition, RoomCatalog.Hub.Shortcut) < InteractionRadius)
        {
            Completed = true;
        }
        else if (Room == RoomCatalog.Hub.Id && PlayerPosition.X > room.Bounds.Right - 40)
        {
            Room = RoomCatalog.Branch.Id;
            PlayerPosition = RoomCatalog.Branch.Spawn;
            PlayerVelocity = Vector2.Zero;
        }
    }

    private void Respawn()
    {
        Health = MaximumHealth;
        Room = checkpointActivated ? RoomCatalog.Branch.Id : CheckpointRoom;
        PlayerPosition = CheckpointPosition;
        PlayerVelocity = Vector2.Zero;
        projectiles.Clear();
        var enemies = CurrentEnemies;
        for (var i = 0; i < enemies.Count; i++)
            enemies[i] = enemies[i] with { Position = CurrentRoom.EnemySpawns[i] };
    }
}
