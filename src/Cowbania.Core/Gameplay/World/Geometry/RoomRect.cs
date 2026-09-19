namespace Cowbania.Core.Gameplay.World.Geometry;

public readonly record struct RoomRect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
}
