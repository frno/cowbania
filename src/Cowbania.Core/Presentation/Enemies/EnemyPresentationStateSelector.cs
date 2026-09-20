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
            EnemyArchetype.Wildlife => PresentationAnimationState.WildlifePatrol,
            EnemyArchetype.DynamiteArmadillo when enemy.BehaviorState == EnemyBehaviorState.Notice =>
                PresentationAnimationState.ArmadilloNotice,
            EnemyArchetype.DynamiteArmadillo when enemy.BehaviorState == EnemyBehaviorState.Attack =>
                PresentationAnimationState.ArmadilloRoll,
            EnemyArchetype.DynamiteArmadillo => PresentationAnimationState.ArmadilloPatrol,
            EnemyArchetype.SidewinderSnake when enemy.BehaviorState == EnemyBehaviorState.Hidden =>
                PresentationAnimationState.SnakeHidden,
            EnemyArchetype.SidewinderSnake when enemy.AttackPhase == EnemyAttackPhase.Telegraph =>
                PresentationAnimationState.SnakeRise,
            EnemyArchetype.SidewinderSnake when enemy.AttackPhase == EnemyAttackPhase.Active =>
                PresentationAnimationState.SnakeExposed,
            EnemyArchetype.SidewinderSnake when enemy.AttackPhase == EnemyAttackPhase.Recovery =>
                PresentationAnimationState.SnakeRetreat,
            _ => PresentationAnimationState.SnakeHidden
        };

        var telegraphMarker = (enemy.Archetype, enemy.AttackPhase, enemy.BehaviorState) switch
        {
            (EnemyArchetype.Bandit, EnemyAttackPhase.Telegraph, _) =>
                EnemyTelegraphMarker.BanditQuickDrawWarning,
            (EnemyArchetype.Wildlife, EnemyAttackPhase.Telegraph, _) =>
                EnemyTelegraphMarker.WildlifeLungeArrow,
            (EnemyArchetype.Bandit, EnemyAttackPhase.Active, _) =>
                EnemyTelegraphMarker.BanditMuzzleFlash,
            (EnemyArchetype.Wildlife, EnemyAttackPhase.Active, _) =>
                EnemyTelegraphMarker.WildlifeLungeTrail,
            (_, _, EnemyBehaviorState.Notice) =>
                EnemyTelegraphMarker.NoticeBurst,
            _ => EnemyTelegraphMarker.None
        };

        return new EnemyPresentationDefinition(
            animationState,
            enemy.Archetype switch
            {
                EnemyArchetype.Bandit => EnemyPaletteTint.Bandit,
                EnemyArchetype.Wildlife => EnemyPaletteTint.Wildlife,
                EnemyArchetype.DynamiteArmadillo => EnemyPaletteTint.Armadillo,
                _ => EnemyPaletteTint.Snake
            },
            telegraphMarker,
            facingDirection,
            enemy.AttackPhase == EnemyAttackPhase.Active &&
            (enemy.Archetype == EnemyArchetype.Bandit || enemy.Archetype == EnemyArchetype.Wildlife));
    }
}
