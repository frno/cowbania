using Cowbania.Core.Gameplay.Enemies;
using Cowbania.Core.Presentation.Animation;

namespace Cowbania.Core.Presentation.Enemies;

public static class EnemyPresentationStateSelector
{
    public static PresentationAnimationState Select(bool alive) =>
        alive ? PresentationAnimationState.EnemyIdle : PresentationAnimationState.Idle;

    public static EnemyPresentationDefinition Select(EnemyState enemy)
    {
        var facingDirection = enemy.FacingDirection < 0 ? -1 : 1;
        if (!enemy.Alive || enemy.BehaviorState == EnemyBehaviorState.Defeated)
        {
            return new EnemyPresentationDefinition(
                PresentationAnimationState.EnemyDefeated,
                EnemyPaletteTint.Defeated,
                EnemyTelegraphMarker.None,
                facingDirection,
                false);
        }

        var animationState = enemy.Archetype switch
        {
            EnemyArchetype.Bandit when enemy.BehaviorState == EnemyBehaviorState.Notice =>
                PresentationAnimationState.BanditNotice,
            EnemyArchetype.Bandit when enemy.BehaviorState == EnemyBehaviorState.Attack =>
                PresentationAnimationState.BanditAttack,
            EnemyArchetype.Bandit => PresentationAnimationState.BanditPatrol,
            EnemyArchetype.Wildlife when enemy.BehaviorState == EnemyBehaviorState.Notice =>
                PresentationAnimationState.WildlifeNotice,
            EnemyArchetype.Wildlife when enemy.BehaviorState == EnemyBehaviorState.Attack =>
                PresentationAnimationState.WildlifeLunge,
            _ => PresentationAnimationState.WildlifePatrol
        };

        var telegraphMarker = enemy.AttackPhase switch
        {
            EnemyAttackPhase.Telegraph when enemy.Archetype == EnemyArchetype.Bandit =>
                EnemyTelegraphMarker.BanditAimLine,
            EnemyAttackPhase.Telegraph => EnemyTelegraphMarker.WildlifeLungeArrow,
            EnemyAttackPhase.Active when enemy.Archetype == EnemyArchetype.Bandit =>
                EnemyTelegraphMarker.BanditMuzzleFlash,
            EnemyAttackPhase.Active => EnemyTelegraphMarker.WildlifeLungeTrail,
            _ when enemy.BehaviorState == EnemyBehaviorState.Notice =>
                EnemyTelegraphMarker.NoticeBurst,
            _ => EnemyTelegraphMarker.None
        };

        return new EnemyPresentationDefinition(
            animationState,
            enemy.Archetype == EnemyArchetype.Bandit
                ? EnemyPaletteTint.Bandit
                : EnemyPaletteTint.Wildlife,
            telegraphMarker,
            facingDirection,
            enemy.AttackPhase == EnemyAttackPhase.Active);
    }
}
