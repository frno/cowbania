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
    string SourceId)
{
    public ProjectileState(Vector2 position, Vector2 velocity, int damage)
        : this(position, velocity, damage, ProjectileOwner.Player, ProjectileKind.Revolver, "player")
    {
    }
}

public enum EnemyArchetype
{
    Bandit,
    Wildlife
}

public enum EnemyBehaviorState
{
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
    ImmutableArray<EnemyDefinition> EnemyDefinitions,
    ImmutableArray<PickupDefinition> Pickups)
{
    public RoomRect Ground => Solids[0];
    public Vector2 Enemy => EnemyDefinitions[0].Spawn;
    public ImmutableArray<Vector2> EnemySpawns =>
        EnemyDefinitions.Select(definition => definition.Spawn).ToImmutableArray();
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
        ImmutableArray.Create(
            new EnemyDefinition("hub-bandit-0", EnemyArchetype.Bandit, new Vector2(520, 480), 120),
            new EnemyDefinition("hub-wildlife-1", EnemyArchetype.Wildlife, new Vector2(1040, 480), 120)),
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
        ImmutableArray.Create(
            new EnemyDefinition("branch-wildlife-0", EnemyArchetype.Wildlife, new Vector2(520, 480), 120),
            new EnemyDefinition("branch-bandit-1", EnemyArchetype.Bandit, new Vector2(1120, 480), 120)),
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
    private const float PlayerHalfWidth = PlayerBodyWidth / 2f;
    private const float EnemyPatrolSpeed = 45f;
    private const float EnemyChaseSpeed = 90f;
    private const float BanditActiveDuration = 0.05f;
    public static readonly Vector2 PlayerMuzzleOffset = new(0, -24);

    private readonly List<ProjectileState> projectiles = new();
    private readonly Dictionary<int, List<EnemyRuntime>> enemiesByRoom = new();
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
    public IReadOnlyList<EnemyState> Enemies => CurrentEnemies.Select(enemy => enemy.Snapshot).ToArray();
    public EnemyState Enemy
    {
        get => CurrentEnemies[0].Snapshot;
        private set => CurrentEnemies[0].ApplySnapshot(value);
    }
    public IReadOnlyList<ProjectileState> Projectiles => projectiles;
    public IEnumerable<PickupDefinition> AvailablePickups =>
        CurrentRoom.Pickups.Where(pickup => !collectedPickupIds.Contains(pickup.Id));

    private List<EnemyRuntime> CurrentEnemies
    {
        get
        {
            if (!enemiesByRoom.TryGetValue(Room, out var enemies))
            {
                var room = CurrentRoom;
                enemies = room.EnemyDefinitions.Select(definition => new EnemyRuntime(definition)).ToList();
                enemiesByRoom[Room] = enemies;
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

        if (UpdateProjectiles(elapsedSeconds))
            return;
        if (UpdateEnemy(elapsedSeconds))
            return;
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

    private bool UpdateProjectiles(float dt)
    {
        for (var i = projectiles.Count - 1; i >= 0; i--)
        {
            var projectile = projectiles[i] with { Position = projectiles[i].Position + projectiles[i].Velocity * dt };
            if (projectile.Owner == ProjectileOwner.Player)
            {
                var hitEnemy = CurrentEnemies.FindIndex(enemy =>
                    enemy.Alive && Vector2.Distance(projectile.Position, enemy.Position) < 30);
                if (hitEnemy >= 0)
                {
                    CurrentEnemies[hitEnemy].Damage(projectile.Damage);
                    projectiles.RemoveAt(i);
                    continue;
                }
            }
            else if (Vector2.Distance(projectile.Position, PlayerPosition + new Vector2(0, -PlayerBodyHeight / 2f)) <
                     PlayerBodyHeight / 2f)
            {
                projectiles.RemoveAt(i);
                if (TryDamagePlayer(projectile.Damage))
                    return true;
                continue;
            }

            if (IsOutsideRoom(projectile.Position) || CurrentRoom.Solids.Any(solid => Contains(solid, projectile.Position)))
                projectiles.RemoveAt(i);
            else
                projectiles[i] = projectile;
        }

        return false;
    }

    private bool UpdateEnemy(float dt)
    {
        var roomId = Room;
        var enemies = CurrentEnemies;
        foreach (var enemy in enemies)
        {
            if (!enemy.Alive)
                continue;

            var distance = Vector2.Distance(PlayerPosition, enemy.Position);
            switch (enemy.BehaviorState)
            {
                case EnemyBehaviorState.Patrol:
                    if (distance <= EnemyDisengageRadius)
                    {
                        enemy.EnterState(EnemyBehaviorState.Notice);
                    }
                    else
                    {
                        MoveEnemy(enemy, enemy.FacingDirection * EnemyPatrolSpeed, dt);
                    }
                    break;

                case EnemyBehaviorState.Notice:
                    if (distance > EnemyDisengageRadius)
                    {
                        enemy.EnterState(EnemyBehaviorState.Patrol);
                        break;
                    }
                    FacePlayer(enemy);
                    enemy.StateElapsed += dt;
                    if (enemy.StateElapsed >= EnemyNoticeDuration)
                        enemy.EnterState(EnemyBehaviorState.Chase);
                    break;

                case EnemyBehaviorState.Chase:
                    if (distance > EnemyDisengageRadius)
                    {
                        enemy.EnterState(EnemyBehaviorState.Patrol);
                        break;
                    }

                    FacePlayer(enemy);
                    if (enemy.Definition.Archetype == EnemyArchetype.Bandit)
                    {
                        var horizontalDistance = MathF.Abs(PlayerPosition.X - enemy.Position.X);
                        if (horizontalDistance >= BanditMinimumAttackRange &&
                            horizontalDistance <= BanditMaximumAttackRange)
                        {
                            BeginAttack(enemy);
                        }
                        else
                        {
                            var direction = horizontalDistance < BanditMinimumAttackRange
                                ? -enemy.FacingDirection
                                : enemy.FacingDirection;
                            MoveEnemy(enemy, direction * EnemyChaseSpeed, dt);
                        }
                    }
                    else if (distance <= WildlifeAttackRange)
                    {
                        BeginAttack(enemy);
                    }
                    else
                    {
                        MoveEnemy(enemy, enemy.FacingDirection * EnemyChaseSpeed, dt);
                    }
                    break;

                case EnemyBehaviorState.Attack:
                    if (distance > EnemyDisengageRadius)
                    {
                        enemy.EnterState(EnemyBehaviorState.Patrol);
                        break;
                    }
                    if (UpdateAttack(enemy, dt))
                        return true;
                    if (Room != roomId)
                        return true;
                    break;
            }
        }

        return false;
    }

    private void BeginAttack(EnemyRuntime enemy)
    {
        enemy.EnterState(EnemyBehaviorState.Attack);
        enemy.AttackPhase = EnemyAttackPhase.Telegraph;
        enemy.AttackElapsed = 0;
        enemy.DamageAppliedThisAttack = false;
        FacePlayer(enemy);
    }

    private bool UpdateAttack(EnemyRuntime enemy, float dt)
    {
        FacePlayer(enemy);
        enemy.StateElapsed += dt;
        enemy.AttackElapsed += dt;

        if (enemy.AttackPhase == EnemyAttackPhase.Telegraph)
        {
            var duration = enemy.Definition.Archetype == EnemyArchetype.Bandit
                ? BanditTelegraphDuration
                : WildlifeTelegraphDuration;
            if (enemy.AttackElapsed < duration)
                return false;

            enemy.AttackPhase = EnemyAttackPhase.Active;
            enemy.AttackElapsed = 0;
            if (enemy.Definition.Archetype == EnemyArchetype.Bandit)
            {
                var velocity = new Vector2(enemy.FacingDirection * BanditProjectileSpeed, 0);
                projectiles.Add(new ProjectileState(
                    enemy.Position + new Vector2(enemy.FacingDirection * PlayerMuzzleDistance, PlayerMuzzleOffset.Y),
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
                ? BanditActiveDuration
                : WildlifeLungeDuration;
            if (enemy.Definition.Archetype == EnemyArchetype.Wildlife)
            {
                MoveEnemy(enemy, enemy.FacingDirection * WildlifeLungeSpeed, dt);
                if (!enemy.DamageAppliedThisAttack &&
                    Vector2.Distance(PlayerPosition, enemy.Position) < 28)
                {
                    enemy.DamageAppliedThisAttack = true;
                    if (TryDamagePlayer(1))
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
            ? BanditRecoveryDuration
            : WildlifeRecoveryDuration;
        if (enemy.AttackElapsed >= recoveryDuration)
            enemy.EnterState(
                Vector2.Distance(PlayerPosition, enemy.Position) > EnemyDisengageRadius
                    ? EnemyBehaviorState.Patrol
                    : EnemyBehaviorState.Chase);
        return false;
    }

    private void MoveEnemy(EnemyRuntime enemy, float horizontalVelocity, float dt)
    {
        var room = CurrentRoom;
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

    private void FacePlayer(EnemyRuntime enemy)
    {
        var delta = PlayerPosition.X - enemy.Position.X;
        if (MathF.Abs(delta) > 0.001f)
            enemy.FacingDirection = Math.Sign(delta);
        enemy.Velocity = Vector2.Zero;
    }

    private bool TryDamagePlayer(int damage)
    {
        if (invulnerabilityTimer > 0)
            return false;

        Health -= damage;
        invulnerabilityTimer = 0.5f;
        if (Health > 0)
            return false;

        Respawn();
        return true;
    }

    private bool IsOutsideRoom(Vector2 position) =>
        position.X < CurrentRoom.Bounds.X ||
        position.X > CurrentRoom.Bounds.Right ||
        position.Y < CurrentRoom.Bounds.Y ||
        position.Y > CurrentRoom.Bounds.Bottom;

    private static bool Contains(RoomRect rect, Vector2 point) =>
        point.X >= rect.X && point.X <= rect.Right &&
        point.Y >= rect.Y && point.Y <= rect.Bottom;

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
            ClearTransientEncounterState();
            ResetEncounter(Room);
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
            ClearTransientEncounterState();
            ResetEncounter(Room);
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
        ClearTransientEncounterState();
        ResetEncounter(Room);
    }

    private void ClearTransientEncounterState() => projectiles.Clear();

    private void ResetEncounter(int roomId)
    {
        var room = RoomCatalog.ForId(roomId);
        enemiesByRoom[roomId] = room.EnemyDefinitions
            .Select(definition => new EnemyRuntime(definition))
            .ToList();
    }

    private sealed class EnemyRuntime
    {
        public EnemyRuntime(EnemyDefinition definition)
        {
            Definition = definition;
            Reset();
        }

        public EnemyDefinition Definition { get; }
        public EnemyBehaviorState BehaviorState { get; set; }
        public EnemyAttackPhase AttackPhase { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public int FacingDirection { get; set; }
        public int Health { get; set; }
        public bool Alive { get; set; }
        public float StateElapsed { get; set; }
        public float AttackElapsed { get; set; }
        public bool DamageAppliedThisAttack { get; set; }

        public EnemyState Snapshot => new(
            Definition.Id,
            Definition.Archetype,
            BehaviorState,
            AttackPhase,
            Position,
            Velocity,
            FacingDirection,
            Health,
            Alive,
            StateProgress,
            AttackProgress);

        private float StateProgress => BehaviorState switch
        {
            EnemyBehaviorState.Notice => Normalize(StateElapsed, EnemyNoticeDuration),
            EnemyBehaviorState.Attack => AttackProgress,
            EnemyBehaviorState.Defeated => 1f,
            _ => 0f
        };

        private float AttackProgress => AttackPhase switch
        {
            EnemyAttackPhase.Telegraph => Normalize(
                AttackElapsed,
                Definition.Archetype == EnemyArchetype.Bandit
                    ? BanditTelegraphDuration
                    : WildlifeTelegraphDuration),
            EnemyAttackPhase.Active => Normalize(
                AttackElapsed,
                Definition.Archetype == EnemyArchetype.Bandit
                    ? BanditActiveDuration
                    : WildlifeLungeDuration),
            EnemyAttackPhase.Recovery => Normalize(
                AttackElapsed,
                Definition.Archetype == EnemyArchetype.Bandit
                    ? BanditRecoveryDuration
                    : WildlifeRecoveryDuration),
            _ => 0f
        };

        public void EnterState(EnemyBehaviorState state)
        {
            BehaviorState = state;
            StateElapsed = 0;
            Velocity = Vector2.Zero;
            if (state != EnemyBehaviorState.Attack)
            {
                AttackPhase = EnemyAttackPhase.None;
                AttackElapsed = 0;
                DamageAppliedThisAttack = false;
            }
        }

        public void Damage(int damage)
        {
            if (!Alive)
                return;

            Health -= damage;
            if (Health > 0)
                return;

            Health = 0;
            Alive = false;
            EnterState(EnemyBehaviorState.Defeated);
        }

        public void ApplySnapshot(EnemyState state)
        {
            Position = state.Position;
            Velocity = state.Velocity;
            FacingDirection = state.FacingDirection is -1 or 1
                ? state.FacingDirection
                : Definition.InitialFacingDirection;
            Health = state.Health;
            Alive = state.Alive;
            BehaviorState = state.Alive ? state.BehaviorState : EnemyBehaviorState.Defeated;
            AttackPhase = state.Alive ? state.AttackPhase : EnemyAttackPhase.None;
            StateElapsed = 0;
            AttackElapsed = 0;
            DamageAppliedThisAttack = false;
        }

        private void Reset()
        {
            Position = Definition.Spawn;
            Velocity = Vector2.Zero;
            FacingDirection = Definition.InitialFacingDirection is -1 or 1
                ? Definition.InitialFacingDirection
                : 1;
            Health = EnemyMaximumHealth;
            Alive = true;
            BehaviorState = EnemyBehaviorState.Patrol;
            AttackPhase = EnemyAttackPhase.None;
            StateElapsed = 0;
            AttackElapsed = 0;
            DamageAppliedThisAttack = false;
        }

        private static float Normalize(float elapsed, float duration) =>
            duration <= 0 ? 1f : Math.Clamp(elapsed / duration, 0f, 1f);
    }
}
