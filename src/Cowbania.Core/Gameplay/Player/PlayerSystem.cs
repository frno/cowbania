using System.Numerics;
using Cowbania.Core.Gameplay.Input;

namespace Cowbania.Core.Gameplay.Player;

internal static class PlayerSystem
{
    internal static bool IsGrounded(GameWorldState state) => state.CurrentRoom.Solids.Any(s =>
        MathF.Abs(state.PlayerPosition.Y - s.Y) < 0.01f &&
        state.PlayerPosition.X + GameWorld.PlayerHalfWidth > s.X &&
        state.PlayerPosition.X - GameWorld.PlayerHalfWidth < s.Right);

    internal static void UpdateInput(GameWorldState state, InputFrame input, float elapsedSeconds)
    {
        state.DashCooldown = MathF.Max(0, state.DashCooldown - elapsedSeconds);
        state.FireTimer = MathF.Max(0, state.FireTimer - elapsedSeconds);
        state.InvulnerabilityTimer = MathF.Max(0, state.InvulnerabilityTimer - elapsedSeconds);
        var horizontal = Math.Clamp(input.Horizontal, -1, 1);
        if (horizontal != 0) state.FacingDirection = horizontal;
        if (input.Aim.LengthSquared() > 0.01f)
        {
            state.AimDirection = Vector2.Normalize(input.Aim);
            if (MathF.Abs(state.AimDirection.X) > 0.01f)
                state.FacingDirection = Math.Sign(state.AimDirection.X);
        }
        else
        {
            state.AimDirection = new Vector2(state.FacingDirection, 0);
        }
        if (input.JumpPressed)
        {
            if (IsGrounded(state) && state.DashTimer <= 0)
            {
                state.PlayerVelocity = new(state.PlayerVelocity.X, GameWorld.JumpSpeed);
                state.LastJumpRequestOutcome = JumpRequestOutcome.Accepted;
            }
            else
            {
                state.LastJumpRequestOutcome = state.DashTimer > 0
                    ? JumpRequestOutcome.RejectedDashing
                    : JumpRequestOutcome.RejectedNotGrounded;
            }
        }
        if (input.DashPressed && state.DashCooldown <= 0 && state.DashTimer <= 0)
        {
            state.DashTimer = GameWorld.DashDuration;
            state.DashCooldown = 0.35f;
        }
    }

    internal static void Move(GameWorldState state, int inputHorizontal, float elapsedSeconds)
    {
        var horizontal = Math.Clamp(inputHorizontal, -1, 1);
        var speed = state.DashTimer > 0 ? GameWorld.DashSpeed : GameWorld.PlayerSpeed;
        var previousFeet = state.PlayerPosition.Y;
        state.PlayerVelocity = new(horizontal * speed, state.PlayerVelocity.Y + GameWorld.Gravity * elapsedSeconds);
        if (state.DashTimer > 0) state.DashTimer -= elapsedSeconds;
        var next = state.PlayerPosition + state.PlayerVelocity * elapsedSeconds;
        next.X = Math.Clamp(
            next.X,
            state.CurrentRoom.Bounds.X + GameWorld.PlayerHalfWidth,
            state.CurrentRoom.Bounds.Right - GameWorld.PlayerHalfWidth);
        ResolveVerticalLanding(state, previousFeet, ref next);
        state.PlayerPosition = next;
    }

    private static void ResolveVerticalLanding(GameWorldState state, float previousFeet, ref Vector2 next)
    {
        if (state.PlayerVelocity.Y < 0) return;
        foreach (var solid in state.CurrentRoom.Solids)
        {
            var overlaps =
                next.X + GameWorld.PlayerHalfWidth > solid.X &&
                next.X - GameWorld.PlayerHalfWidth < solid.Right;
            if (overlaps && previousFeet <= solid.Y && next.Y >= solid.Y)
            {
                next.Y = solid.Y;
                state.PlayerVelocity = new(state.PlayerVelocity.X, 0);
                break;
            }
        }
    }
}
