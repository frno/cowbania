using System.Numerics;

namespace Cowbania.Core.Gameplay.World.Geometry;

internal static class CollisionQueries
{
    public static bool Contains(RoomRect rect, Vector2 point) =>
        point.X >= rect.X && point.X <= rect.Right &&
        point.Y >= rect.Y && point.Y <= rect.Bottom;
}
