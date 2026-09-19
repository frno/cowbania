using Microsoft.Xna.Framework;
using NumericsVector2 = System.Numerics.Vector2;

namespace Cowbania.Host.Presentation.Camera;

internal sealed class GameCamera
{
    public float X { get; private set; }

    public void Follow(RoomDefinition room, NumericsVector2 playerPosition, int viewportWidth)
    {
        var maximum = Math.Max(room.Bounds.X, room.Bounds.Right - viewportWidth);
        X = Math.Clamp(playerPosition.X - viewportWidth / 2f, room.Bounds.X, maximum);
    }

    public Vector2 ToScreen(NumericsVector2 position) => new(position.X - X, position.Y);
    public Rectangle ToScreen(RoomRect rectangle) =>
        new((int)(rectangle.X - X), (int)rectangle.Y, (int)rectangle.Width, (int)rectangle.Height);
}
