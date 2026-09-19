using Microsoft.Xna.Framework;

namespace Cowbania.Host.Presentation.Rendering;

internal readonly record struct StagePalette(Color SkyTop, Color SkyBottom, Color GroundTop)
{
    private static readonly StagePalette Hub = new(
        new Color(24, 30, 46),
        new Color(133, 156, 177),
        new Color(32, 109, 82));

    private static readonly StagePalette Branch = new(
        new Color(18, 24, 38),
        new Color(104, 120, 154),
        new Color(44, 72, 77));

    public static StagePalette For(int roomId) => roomId == RoomCatalog.Branch.Id ? Branch : Hub;
}
