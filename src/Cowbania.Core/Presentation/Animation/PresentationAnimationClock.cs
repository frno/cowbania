using Cowbania.Core.Gameplay.Enemies;
using Cowbania.Core.Gameplay.Pickups;
using Cowbania.Core.Presentation.Enemies;

namespace Cowbania.Core.Presentation.Animation;

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
        var state = EnemyPresentationStateSelector.Select(enemy).AnimationState;
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
