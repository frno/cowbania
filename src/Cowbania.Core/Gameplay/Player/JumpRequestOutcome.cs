namespace Cowbania.Core.Gameplay.Player;

public enum JumpRequestOutcome
{
    None,
    Accepted,
    RejectedPaused,
    RejectedCompleted,
    RejectedNotGrounded,
    RejectedDashing
}
