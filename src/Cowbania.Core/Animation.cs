using System.Collections.Immutable;

namespace Cowbania.Core;

public enum AnimationPlaybackMode
{
    Loop,
    OneShot
}

public readonly record struct AnimationFrame(string AssetKey, float DurationSeconds)
{
    public AnimationFrame(string assetKey) : this(assetKey, 0f)
    {
    }
}

public sealed record AnimationClip(
    string Name,
    ImmutableArray<AnimationFrame> Frames,
    float FramesPerSecond,
    AnimationPlaybackMode PlaybackMode = AnimationPlaybackMode.Loop)
{
    public AnimationClip(
        string name,
        IEnumerable<AnimationFrame> frames,
        float framesPerSecond,
        AnimationPlaybackMode playbackMode = AnimationPlaybackMode.Loop)
        : this(name, frames.ToImmutableArray(), framesPerSecond, playbackMode)
    {
    }

    public float FrameDuration =>
        FramesPerSecond > 0f ? 1f / FramesPerSecond : 0f;
}

public sealed class AnimationClock
{
    public int CurrentFrameIndex { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public bool IsComplete { get; private set; }

    public void Reset()
    {
        CurrentFrameIndex = 0;
        ElapsedSeconds = 0f;
        IsComplete = false;
    }

    public void Advance(float elapsedSeconds, AnimationClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (clip.Frames.IsDefaultOrEmpty || elapsedSeconds <= 0f || IsComplete)
            return;

        var frameDuration = clip.FrameDuration;
        if (frameDuration <= 0f)
            return;

        ElapsedSeconds += elapsedSeconds;
        var frameCount = clip.Frames.Length;
        var frameAdvance = (int)MathF.Floor(ElapsedSeconds / frameDuration);
        if (frameAdvance <= 0)
            return;

        ElapsedSeconds -= frameAdvance * frameDuration;
        if (clip.PlaybackMode == AnimationPlaybackMode.Loop)
        {
            CurrentFrameIndex = (CurrentFrameIndex + frameAdvance) % frameCount;
            return;
        }

        var nextIndex = CurrentFrameIndex + frameAdvance;
        if (nextIndex >= frameCount)
        {
            CurrentFrameIndex = frameCount - 1;
            IsComplete = true;
            ElapsedSeconds = 0f;
        }
        else
        {
            CurrentFrameIndex = nextIndex;
        }
    }

    public AnimationFrame CurrentFrame(AnimationClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (clip.Frames.IsDefaultOrEmpty)
            throw new InvalidOperationException($"Animation clip '{clip.Name}' has no frames.");

        return clip.Frames[Math.Clamp(CurrentFrameIndex, 0, clip.Frames.Length - 1)];
    }
}

public enum PlayerAnimationState
{
    Idle,
    Run,
    Jump,
    Fall,
    Shoot,
    Reload,
    Hurt,
    Dash
}

public readonly record struct ActorPresentationState(
    PlayerAnimationState AnimationState,
    System.Numerics.Vector2 FeetAnchor,
    System.Numerics.Vector2 MuzzleAnchor,
    int FacingDirection);

public static class PlayerAnimationStateSelector
{
    public static PlayerAnimationState Select(
        System.Numerics.Vector2 velocity,
        bool grounded,
        bool shooting = false,
        bool reloading = false,
        bool hurt = false,
        bool dashing = false)
    {
        if (hurt) return PlayerAnimationState.Hurt;
        if (dashing) return PlayerAnimationState.Dash;
        if (reloading) return PlayerAnimationState.Reload;
        if (shooting) return PlayerAnimationState.Shoot;
        if (!grounded) return velocity.Y < 0 ? PlayerAnimationState.Jump : PlayerAnimationState.Fall;
        return MathF.Abs(velocity.X) > 0.01f ? PlayerAnimationState.Run : PlayerAnimationState.Idle;
    }
}

public enum PresentationAnimationState
{
    Idle,
    Run,
    Jump,
    Fall,
    Shoot,
    Reload,
    Hurt,
    Dash,
    EnemyIdle,
    PickupFloat,
    BanditPatrol,
    BanditNotice,
    BanditAttack,
    WildlifePatrol,
    WildlifeNotice,
    WildlifeLunge,
    EnemyDefeated
}

public readonly record struct PlayerPresentationInput(
    bool Moving,
    bool Grounded,
    bool Rising,
    bool Shooting,
    bool Reloading,
    bool Hurt,
    bool Dashing);

public enum EnemyTelegraphMarker
{
    None,
    NoticeBurst,
    BanditAimLine,
    BanditMuzzleFlash,
    WildlifeLungeArrow,
    WildlifeLungeTrail
}

public readonly record struct EnemyPaletteTint(byte Red, byte Green, byte Blue)
{
    public static EnemyPaletteTint Bandit { get; } = new(218, 164, 94);
    public static EnemyPaletteTint Wildlife { get; } = new(139, 190, 105);
    public static EnemyPaletteTint Defeated { get; } = new(110, 104, 100);
}

public readonly record struct EnemyPresentationDefinition(
    PresentationAnimationState AnimationState,
    EnemyPaletteTint PaletteTint,
    EnemyTelegraphMarker TelegraphMarker,
    int FacingDirection,
    bool AttackActive);

public static class PresentationStateSelector
{
    public static PresentationAnimationState SelectPlayer(PlayerPresentationInput input)
    {
        if (input.Hurt) return PresentationAnimationState.Hurt;
        if (input.Reloading) return PresentationAnimationState.Reload;
        if (input.Shooting) return PresentationAnimationState.Shoot;
        if (input.Dashing) return PresentationAnimationState.Dash;
        if (!input.Grounded) return input.Rising
            ? PresentationAnimationState.Jump
            : PresentationAnimationState.Fall;
        return input.Moving ? PresentationAnimationState.Run : PresentationAnimationState.Idle;
    }

    public static PresentationAnimationState SelectEnemy(bool alive) =>
        alive ? PresentationAnimationState.EnemyIdle : PresentationAnimationState.Idle;

    public static EnemyPresentationDefinition SelectEnemy(EnemyState enemy)
    {
        var facingDirection = enemy.FacingDirection < 0 ? -1 : 1;
        if (!enemy.Alive || enemy.BehaviorState == EnemyBehaviorState.Defeated)
        {
            return new EnemyPresentationDefinition(
                PresentationAnimationState.EnemyDefeated,
                EnemyPaletteTint.Defeated,
                EnemyTelegraphMarker.None,
                facingDirection,
                false);
        }

        var animationState = enemy.Archetype switch
        {
            EnemyArchetype.Bandit when enemy.BehaviorState == EnemyBehaviorState.Notice =>
                PresentationAnimationState.BanditNotice,
            EnemyArchetype.Bandit when enemy.BehaviorState == EnemyBehaviorState.Attack =>
                PresentationAnimationState.BanditAttack,
            EnemyArchetype.Bandit => PresentationAnimationState.BanditPatrol,
            EnemyArchetype.Wildlife when enemy.BehaviorState == EnemyBehaviorState.Notice =>
                PresentationAnimationState.WildlifeNotice,
            EnemyArchetype.Wildlife when enemy.BehaviorState == EnemyBehaviorState.Attack =>
                PresentationAnimationState.WildlifeLunge,
            _ => PresentationAnimationState.WildlifePatrol
        };

        var telegraphMarker = enemy.AttackPhase switch
        {
            EnemyAttackPhase.Telegraph when enemy.Archetype == EnemyArchetype.Bandit =>
                EnemyTelegraphMarker.BanditAimLine,
            EnemyAttackPhase.Telegraph => EnemyTelegraphMarker.WildlifeLungeArrow,
            EnemyAttackPhase.Active when enemy.Archetype == EnemyArchetype.Bandit =>
                EnemyTelegraphMarker.BanditMuzzleFlash,
            EnemyAttackPhase.Active => EnemyTelegraphMarker.WildlifeLungeTrail,
            _ when enemy.BehaviorState == EnemyBehaviorState.Notice =>
                EnemyTelegraphMarker.NoticeBurst,
            _ => EnemyTelegraphMarker.None
        };

        return new EnemyPresentationDefinition(
            animationState,
            enemy.Archetype == EnemyArchetype.Bandit
                ? EnemyPaletteTint.Bandit
                : EnemyPaletteTint.Wildlife,
            telegraphMarker,
            facingDirection,
            enemy.AttackPhase == EnemyAttackPhase.Active);
    }

    public static PresentationAnimationState SelectPickup() =>
        PresentationAnimationState.PickupFloat;
}

public sealed class PresentationAnimationClock
{
    private readonly AnimationClock clock = new();

    public PresentationAnimationState? CurrentState { get; private set; }
    public int CurrentFrameIndex => clock.CurrentFrameIndex;
    public bool IsComplete => clock.IsComplete;

    public void Reset()
    {
        CurrentState = null;
        clock.Reset();
    }

    public void Advance(float elapsedSeconds, PresentationAnimationState state)
    {
        if (CurrentState != state)
        {
            CurrentState = state;
            clock.Reset();
        }

        clock.Advance(elapsedSeconds, PlaceholderAnimationCatalog.For(state));
    }

    public AnimationFrame CurrentFrame()
    {
        if (CurrentState is not { } state)
            throw new InvalidOperationException("Select an animation state before reading its frame.");

        return clock.CurrentFrame(PlaceholderAnimationCatalog.For(state));
    }
}

public static class PlaceholderAnimationCatalog
{
    private static readonly AnimationClip Idle = new("idle", new[] { new AnimationFrame("idle_0.png"), new("idle_1.png") }, 3);
    private static readonly AnimationClip Run = new("run", new[] { new AnimationFrame("run_0.png"), new("run_1.png") }, 8);
    private static readonly AnimationClip Reload = new("reload", new[] { new AnimationFrame("reload_0.png"), new("reload_1.png") }, 6, AnimationPlaybackMode.OneShot);
    private static readonly AnimationClip Shoot = new("shoot", new[] { new AnimationFrame("shoot_0.png"), new("shoot_1.png") }, 12, AnimationPlaybackMode.OneShot);
    private static readonly AnimationClip Jump = new("jump", new[] { new AnimationFrame("jump_0.png") }, 1);
    private static readonly AnimationClip Fall = new("fall", new[] { new AnimationFrame("fall_0.png") }, 1);
    private static readonly AnimationClip Hurt = new("hurt", new[] { new AnimationFrame("hurt_0.png") }, 1, AnimationPlaybackMode.OneShot);
    private static readonly AnimationClip EnemyIdle = new("enemy_idle", new[] { new AnimationFrame("idle_0.png"), new("idle_1.png") }, 4);
    private static readonly AnimationClip PickupFloat = new("pickup_float", new[] { new AnimationFrame("float_0.png"), new("float_1.png") }, 4);
    private static readonly AnimationClip BanditPatrol = new("bandit_patrol", new[] { new AnimationFrame("idle_0.png"), new("idle_1.png") }, 4);
    private static readonly AnimationClip BanditNotice = new("bandit_notice", new[] { new AnimationFrame("idle_1.png"), new("idle_0.png") }, 8, AnimationPlaybackMode.OneShot);
    private static readonly AnimationClip BanditAttack = new("bandit_attack", new[] { new AnimationFrame("idle_1.png"), new("idle_0.png") }, 10);
    private static readonly AnimationClip WildlifePatrol = new("wildlife_patrol", new[] { new AnimationFrame("idle_1.png"), new("idle_0.png") }, 6);
    private static readonly AnimationClip WildlifeNotice = new("wildlife_notice", new[] { new AnimationFrame("idle_0.png"), new("idle_1.png") }, 8, AnimationPlaybackMode.OneShot);
    private static readonly AnimationClip WildlifeLunge = new("wildlife_lunge", new[] { new AnimationFrame("idle_1.png") }, 1);
    private static readonly AnimationClip EnemyDefeated = new("enemy_defeated", new[] { new AnimationFrame("idle_0.png") }, 1, AnimationPlaybackMode.OneShot);

    public static AnimationClip For(PresentationAnimationState state) => state switch
    {
        PresentationAnimationState.Run => Run,
        PresentationAnimationState.Reload => Reload,
        PresentationAnimationState.Shoot => Shoot,
        PresentationAnimationState.Jump => Jump,
        PresentationAnimationState.Fall => Fall,
        PresentationAnimationState.Hurt => Hurt,
        PresentationAnimationState.EnemyIdle => EnemyIdle,
        PresentationAnimationState.PickupFloat => PickupFloat,
        PresentationAnimationState.BanditPatrol => BanditPatrol,
        PresentationAnimationState.BanditNotice => BanditNotice,
        PresentationAnimationState.BanditAttack => BanditAttack,
        PresentationAnimationState.WildlifePatrol => WildlifePatrol,
        PresentationAnimationState.WildlifeNotice => WildlifeNotice,
        PresentationAnimationState.WildlifeLunge => WildlifeLunge,
        PresentationAnimationState.EnemyDefeated => EnemyDefeated,
        _ => Idle
    };
}
