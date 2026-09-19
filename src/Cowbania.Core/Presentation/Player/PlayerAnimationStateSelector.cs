using System.Numerics;

namespace Cowbania.Core.Presentation.Player;

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
