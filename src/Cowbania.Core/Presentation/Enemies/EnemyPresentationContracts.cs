using Cowbania.Core.Presentation.Animation;

namespace Cowbania.Core.Presentation.Enemies;

public enum EnemyTelegraphMarker
{
    None,
    NoticeBurst,
    BanditQuickDrawWarning,
    BanditMuzzleFlash,
    WildlifeLungeArrow,
    WildlifeLungeTrail
}

public readonly record struct EnemyPaletteTint(byte Red, byte Green, byte Blue)
{
    public static EnemyPaletteTint Bandit { get; } = new(218, 164, 94);
    public static EnemyPaletteTint Wildlife { get; } = new(139, 190, 105);
    public static EnemyPaletteTint Armadillo { get; } = new(207, 147, 73);
    public static EnemyPaletteTint Snake { get; } = new(188, 204, 132);
    public static EnemyPaletteTint Defeated { get; } = new(110, 104, 100);
}

public readonly record struct EnemyPresentationDefinition(
    PresentationAnimationState AnimationState,
    EnemyPaletteTint PaletteTint,
    EnemyTelegraphMarker TelegraphMarker,
    int FacingDirection,
    bool AttackActive);
