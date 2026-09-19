using System.Numerics;
namespace Cowbania.Core.Presentation.Player;

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

public readonly record struct PlayerPresentationInput(
    bool Moving,
    bool Grounded,
    bool Rising,
    bool Shooting,
    bool Reloading,
    bool Hurt,
    bool Dashing);
