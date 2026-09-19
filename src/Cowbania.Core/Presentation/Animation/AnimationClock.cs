namespace Cowbania.Core.Presentation.Animation;

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
