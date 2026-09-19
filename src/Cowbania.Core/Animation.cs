using System.Collections.Immutable;
using System.Numerics;

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

public readonly record struct AnimationActorMetadata(
    Vector2 SourceFeetAnchor,
    Vector2 SourceEffectAnchor);

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
    Vector2 FeetAnchor,
    Vector2 MuzzleAnchor,
    int FacingDirection);

public static class PlayerAnimationStateSelector
{
    public static PlayerAnimationState Select(
        Vector2 velocity,
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
    private AnimationClip? currentClip;

    public PresentationAnimationState? CurrentState { get; private set; }
    public AnimationClip? CurrentClip => currentClip;
    public int CurrentFrameIndex => clock.CurrentFrameIndex;
    public bool IsComplete => clock.IsComplete;

    public void Reset()
    {
        CurrentState = null;
        currentClip = null;
        clock.Reset();
    }

    public void Advance(float elapsedSeconds, PresentationAnimationState state) =>
        Advance(elapsedSeconds, state, FrontierAnimationCatalog.For(state));

    public void Advance(float elapsedSeconds, EnemyState enemy)
    {
        var state = PresentationStateSelector.SelectEnemy(enemy).AnimationState;
        Advance(elapsedSeconds, state, FrontierAnimationCatalog.ForEnemy(enemy));
    }

    public void Advance(float elapsedSeconds, PickupType pickupType) =>
        Advance(
            elapsedSeconds,
            PresentationAnimationState.PickupFloat,
            FrontierAnimationCatalog.ForPickup(pickupType));

    private void Advance(
        float elapsedSeconds,
        PresentationAnimationState state,
        AnimationClip clip)
    {
        if (CurrentState != state || !ReferenceEquals(currentClip, clip))
        {
            CurrentState = state;
            currentClip = clip;
            clock.Reset();
        }

        clock.Advance(elapsedSeconds, clip);
    }

    public AnimationFrame CurrentFrame()
    {
        if (currentClip is null)
            throw new InvalidOperationException("Select an animation state before reading its frame.");

        return clock.CurrentFrame(currentClip);
    }
}

public static class FrontierAnimationCatalog
{
    public static AnimationActorMetadata PlayerMetadata { get; } =
        new(new Vector2(8, 13), new Vector2(13, 7));

    public static AnimationActorMetadata BanditMetadata { get; } =
        new(new Vector2(8, 13), new Vector2(14, 7));

    public static AnimationActorMetadata WildlifeMetadata { get; } =
        new(new Vector2(8, 13), new Vector2(14, 9));

    public static AnimationActorMetadata PickupMetadata { get; } =
        new(new Vector2(8, 13), new Vector2(8, 8));

    public static ImmutableDictionary<PresentationAnimationState, AnimationClip> PlayerClips { get; } =
        new Dictionary<PresentationAnimationState, AnimationClip>
        {
            [PresentationAnimationState.Idle] = Clip("player_idle", "Frontier/Player", "idle", 4, 6f),
            [PresentationAnimationState.Run] = Clip("player_run", "Frontier/Player", "run", 6, 12f),
            [PresentationAnimationState.Jump] = Clip("player_jump", "Frontier/Player", "jump", 2, 8f),
            [PresentationAnimationState.Fall] = Clip("player_fall", "Frontier/Player", "fall", 2, 8f),
            [PresentationAnimationState.Shoot] = Clip("player_shoot", "Frontier/Player", "shoot", 3, 15f, AnimationPlaybackMode.OneShot),
            [PresentationAnimationState.Reload] = Clip("player_reload", "Frontier/Player", "reload", 4, 4f / GameWorld.ReloadDuration, AnimationPlaybackMode.OneShot),
            [PresentationAnimationState.Hurt] = Clip("player_hurt", "Frontier/Player", "hurt", 2, 10f, AnimationPlaybackMode.OneShot),
            [PresentationAnimationState.Dash] = Clip("player_dash", "Frontier/Player", "dash", 3, 15f, AnimationPlaybackMode.OneShot)
        }.ToImmutableDictionary();

    public static ImmutableDictionary<PresentationAnimationState, AnimationClip> BanditClips { get; } =
        new Dictionary<PresentationAnimationState, AnimationClip>
        {
            [PresentationAnimationState.BanditPatrol] = Clip("bandit_patrol", "Frontier/Bandit", "patrol", 4, 6f),
            [PresentationAnimationState.BanditNotice] = Clip("bandit_notice", "Frontier/Bandit", "notice", 2, 8f, AnimationPlaybackMode.OneShot),
            [PresentationAnimationState.BanditAttack] = Clip("bandit_attack", "Frontier/Bandit", "attack", 4, 12f, AnimationPlaybackMode.OneShot),
            [PresentationAnimationState.EnemyDefeated] = Clip("bandit_defeated", "Frontier/Bandit", "defeated", 2, 6f, AnimationPlaybackMode.OneShot)
        }.ToImmutableDictionary();

    public static ImmutableDictionary<PresentationAnimationState, AnimationClip> WildlifeClips { get; } =
        new Dictionary<PresentationAnimationState, AnimationClip>
        {
            [PresentationAnimationState.WildlifePatrol] = Clip("wildlife_patrol", "Frontier/Wildlife", "patrol", 4, 8f),
            [PresentationAnimationState.WildlifeNotice] = Clip("wildlife_notice", "Frontier/Wildlife", "notice", 2, 8f, AnimationPlaybackMode.OneShot),
            [PresentationAnimationState.WildlifeLunge] = Clip("wildlife_lunge", "Frontier/Wildlife", "lunge", 4, 12f, AnimationPlaybackMode.OneShot),
            [PresentationAnimationState.EnemyDefeated] = Clip("wildlife_defeated", "Frontier/Wildlife", "defeated", 2, 6f, AnimationPlaybackMode.OneShot)
        }.ToImmutableDictionary();

    public static ImmutableDictionary<PickupType, AnimationClip> PickupClips { get; } =
        new Dictionary<PickupType, AnimationClip>
        {
            [PickupType.Currency] = Clip("currency_float", "Frontier/Pickup/Currency", "float", 4, 6f),
            [PickupType.Health] = Clip("health_float", "Frontier/Pickup/Health", "float", 4, 6f),
            [PickupType.ReserveAmmo] = Clip("ammo_float", "Frontier/Pickup/Ammo", "float", 4, 6f)
        }.ToImmutableDictionary();

    public static AnimationClip For(PresentationAnimationState state) => state switch
    {
        PresentationAnimationState.EnemyIdle => BanditClips[PresentationAnimationState.BanditPatrol],
        PresentationAnimationState.PickupFloat => PickupClips[PickupType.Currency],
        PresentationAnimationState.BanditPatrol or
        PresentationAnimationState.BanditNotice or
        PresentationAnimationState.BanditAttack or
        PresentationAnimationState.EnemyDefeated => BanditClips[state],
        PresentationAnimationState.WildlifePatrol or
        PresentationAnimationState.WildlifeNotice or
        PresentationAnimationState.WildlifeLunge => WildlifeClips[state],
        _ => PlayerClips[state]
    };

    public static AnimationClip ForEnemy(EnemyState enemy)
    {
        var state = PresentationStateSelector.SelectEnemy(enemy).AnimationState;
        return enemy.Archetype == EnemyArchetype.Bandit
            ? BanditClips[state]
            : WildlifeClips[state];
    }

    public static AnimationClip ForPickup(PickupType type) => PickupClips[type];

    private static AnimationClip Clip(
        string name,
        string path,
        string frameName,
        int frameCount,
        float framesPerSecond,
        AnimationPlaybackMode playbackMode = AnimationPlaybackMode.Loop) =>
        new(
            name,
            Enumerable.Range(0, frameCount)
                .Select(index => new AnimationFrame($"{path}/{frameName}_{index}.png")),
            framesPerSecond,
            playbackMode);
}

[Obsolete("Use FrontierAnimationCatalog.")]
public static class PlaceholderAnimationCatalog
{
    public static AnimationClip For(PresentationAnimationState state) =>
        FrontierAnimationCatalog.For(state);
}
