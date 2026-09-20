using System.Collections.Immutable;
using System.Numerics;

namespace Cowbania.Core.Presentation.Animation;

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
    ArmadilloPatrol,
    ArmadilloNotice,
    ArmadilloRoll,
    SnakeHidden,
    SnakeRise,
    SnakeExposed,
    SnakeRetreat,
    EnemyDefeated
}
