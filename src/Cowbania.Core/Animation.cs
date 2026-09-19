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
    PickupFloat
}

public readonly record struct PlayerPresentationInput(
    bool Moving,
    bool Grounded,
    bool Rising,
    bool Shooting,
    bool Reloading,
    bool Hurt,
    bool Dashing);

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

    public static PresentationAnimationState SelectPickup() =>
        PresentationAnimationState.PickupFloat;
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
        _ => Idle
    };
}
