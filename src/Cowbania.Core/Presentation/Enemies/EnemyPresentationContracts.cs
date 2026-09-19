using Cowbania.Core.Presentation.Animation;

namespace Cowbania.Core.Presentation.Enemies;

public enum EnemyTelegraphMarker
{
    None,
    NoticeBurst,
    BanditAimLine,
    BanditMuzzleFlash,
    WildlifeLungeArrow,
    WildlifeLungeTrail
}

public readonly record struct EnemyPaletteTint(byte Red, byte Green, byte Blue)
{
    public static EnemyPaletteTint Bandit { get; } = new(218, 164, 94);
    public static EnemyPaletteTint Wildlife { get; } = new(139, 190, 105);
    public static EnemyPaletteTint Defeated { get; } = new(110, 104, 100);
}

public readonly record struct EnemyPresentationDefinition(
    PresentationAnimationState AnimationState,
    EnemyPaletteTint PaletteTint,
    EnemyTelegraphMarker TelegraphMarker,
    int FacingDirection,
    bool AttackActive);
