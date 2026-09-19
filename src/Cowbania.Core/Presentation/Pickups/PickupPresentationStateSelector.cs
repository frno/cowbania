using Cowbania.Core.Presentation.Animation;

namespace Cowbania.Core.Presentation.Pickups;

public static class PickupPresentationStateSelector
{
    public static PresentationAnimationState Select() =>
        PresentationAnimationState.PickupFloat;
}
