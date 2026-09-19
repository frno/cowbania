using Cowbania.Core.Presentation.Animation;

namespace Cowbania.Core.Presentation.Player;

public static class PlayerPresentationStateSelector
{
    public static PresentationAnimationState Select(PlayerPresentationInput input)
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
}
