using System.Numerics;

namespace Cowbania.Core.Gameplay.Input;

public readonly record struct InputFrame(
    int Horizontal,
    bool JumpPressed,
    bool DashPressed,
    Vector2 Aim,
    bool FireHeld,
    bool ReloadPressed,
    bool InteractPressed,
    bool PausePressed,
    bool SelectSlot1Pressed = false);
