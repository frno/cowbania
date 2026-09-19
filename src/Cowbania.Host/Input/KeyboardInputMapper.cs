using Microsoft.Xna.Framework.Input;
using NumericsVector2 = System.Numerics.Vector2;

namespace Cowbania.Host.Input;

internal sealed class KeyboardInputMapper
{
    private KeyboardState previous;

    public InputFrame Read()
    {
        var keyboard = Keyboard.GetState();
        var aim = NumericsVector2.Zero;
        if (keyboard.IsKeyDown(Keys.Up)) aim.Y--;
        if (keyboard.IsKeyDown(Keys.Down)) aim.Y++;
        if (keyboard.IsKeyDown(Keys.Left)) aim.X--;
        if (keyboard.IsKeyDown(Keys.Right)) aim.X++;

        var input = new InputFrame(
            (keyboard.IsKeyDown(Keys.D) ? 1 : 0) - (keyboard.IsKeyDown(Keys.A) ? 1 : 0),
            Pressed(keyboard, Keys.Space),
            Pressed(keyboard, Keys.LeftShift),
            aim,
            keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl),
            Pressed(keyboard, Keys.R),
            Pressed(keyboard, Keys.E),
            Pressed(keyboard, Keys.Escape),
            Pressed(keyboard, Keys.D1));
        previous = keyboard;
        return input;
    }

    private bool Pressed(KeyboardState state, Keys key) =>
        state.IsKeyDown(key) && !previous.IsKeyDown(key);
}
