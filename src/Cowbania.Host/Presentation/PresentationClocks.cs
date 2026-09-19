namespace Cowbania.Host.Presentation;

internal static class PlayerAnimationRestart
{
    public static bool ShouldReset(
        PresentationAnimationState previousState,
        PresentationAnimationState selectedState,
        bool acceptedPlayerShot) =>
        selectedState != previousState ||
        acceptedPlayerShot && selectedState == PresentationAnimationState.Shoot;
}

internal sealed class BoundedEffectClock(int frameCount, float framesPerSecond)
{
    private float elapsedSeconds;

    public int CurrentFrameIndex =>
        Math.Min(frameCount - 1, (int)(elapsedSeconds * framesPerSecond));
    public bool IsVisible => elapsedSeconds < frameCount / framesPerSecond;

    public void Advance(float seconds)
    {
        if (seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        elapsedSeconds = Math.Min(frameCount / framesPerSecond, elapsedSeconds + seconds);
    }
}
