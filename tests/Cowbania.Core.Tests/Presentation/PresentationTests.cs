namespace Cowbania.Core.Tests.Presentation;

internal static class PresentationTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("presentation selector prioritizes transient and locomotion states", () =>
            {
                Assert(PlayerPresentationStateSelector.Select(new PlayerPresentationInput(false, true, false, false, false, false, false)) == PresentationAnimationState.Idle, "idle selection");
                            Assert(PlayerPresentationStateSelector.Select(new PlayerPresentationInput(true, true, false, false, false, false, false)) == PresentationAnimationState.Run, "run selection");
                            Assert(PlayerPresentationStateSelector.Select(new PlayerPresentationInput(false, false, true, false, false, false, false)) == PresentationAnimationState.Jump, "jump selection");
                            Assert(PlayerPresentationStateSelector.Select(new PlayerPresentationInput(false, false, false, false, false, false, false)) == PresentationAnimationState.Fall, "fall selection");
                            Assert(PlayerPresentationStateSelector.Select(new PlayerPresentationInput(true, true, false, true, true, true, true)) == PresentationAnimationState.Hurt, "hurt has priority");
                            Assert(EnemyPresentationStateSelector.Select(true) == PresentationAnimationState.EnemyIdle, "enemy selection");
                            Assert(PickupPresentationStateSelector.Select() == PresentationAnimationState.PickupFloat, "pickup selection");
            });
            yield return new TestCase("enemy presentation selects readable deterministic telegraphs", () =>
            {
                var banditTelegraph = EnemyPresentationStateSelector.Select(new EnemyState(
                                "bandit", EnemyArchetype.Bandit, EnemyBehaviorState.Attack, EnemyAttackPhase.Telegraph,
                                Vector2.Zero, Vector2.Zero, -1, 2, true, 0.5f, 0.25f));
                            var wildlifeActive = EnemyPresentationStateSelector.Select(new EnemyState(
                                "wildlife", EnemyArchetype.Wildlife, EnemyBehaviorState.Attack, EnemyAttackPhase.Active,
                                Vector2.Zero, Vector2.UnitX, 1, 2, true, 0.5f, 0.25f));
                            var defeated = EnemyPresentationStateSelector.Select(new EnemyState(
                                "defeated", EnemyArchetype.Bandit, EnemyBehaviorState.Defeated, EnemyAttackPhase.None,
                                Vector2.Zero, Vector2.Zero, 0, 0, false, 1f, 0f));

                            Assert(banditTelegraph.AnimationState == PresentationAnimationState.BanditAttack &&
                                   banditTelegraph.TelegraphMarker == EnemyTelegraphMarker.BanditAimLine &&
                                   banditTelegraph.FacingDirection == -1 &&
                                   !banditTelegraph.AttackActive,
                                "bandit telegraph selects its ranged aim cue without becoming active");
                            Assert(wildlifeActive.AnimationState == PresentationAnimationState.WildlifeLunge &&
                                   wildlifeActive.TelegraphMarker == EnemyTelegraphMarker.WildlifeLungeTrail &&
                                   wildlifeActive.AttackActive,
                                "wildlife active attack selects its lunge trail and active flag");
                            Assert(defeated.AnimationState == PresentationAnimationState.EnemyDefeated &&
                                   defeated.TelegraphMarker == EnemyTelegraphMarker.None &&
                                   defeated.FacingDirection == 1,
                                "defeated enemies suppress telegraphs and normalize facing");
                            Assert(banditTelegraph.PaletteTint != wildlifeActive.PaletteTint,
                                "bandit and wildlife use stable distinct palette tints");
            });
            yield return new TestCase("player animation selector covers idle run jump fall shoot reload hurt and dash", () =>
            {
                Assert(PlayerAnimationStateSelector.Select(Vector2.Zero, true) == PlayerAnimationState.Idle,
                                "stationary grounded player selects idle");
                            Assert(PlayerAnimationStateSelector.Select(new Vector2(1, 0), true) == PlayerAnimationState.Run,
                                "moving grounded player selects run");
                            Assert(PlayerAnimationStateSelector.Select(new Vector2(0, -1), false) == PlayerAnimationState.Jump,
                                "rising airborne player selects jump");
                            Assert(PlayerAnimationStateSelector.Select(new Vector2(0, 1), false) == PlayerAnimationState.Fall,
                                "descending airborne player selects fall");
                            Assert(PlayerAnimationStateSelector.Select(Vector2.Zero, true, shooting: true) == PlayerAnimationState.Shoot,
                                "shooting player selects shoot");
                            Assert(PlayerAnimationStateSelector.Select(Vector2.Zero, true, reloading: true) == PlayerAnimationState.Reload,
                                "reloading player selects reload");
                            Assert(PlayerAnimationStateSelector.Select(Vector2.Zero, true, hurt: true) == PlayerAnimationState.Hurt,
                                "hurt player selects hurt");
                            Assert(PlayerAnimationStateSelector.Select(Vector2.Zero, true, dashing: true) == PlayerAnimationState.Dash,
                                "dashing player selects dash");
            });
            yield return new TestCase("presentation snapshots preserve feet facing and muzzle anchors", () =>
            {
                var feet = new Vector2(80, 480);
                            var muzzle = feet + GameWorld.PlayerMuzzleOffset + Vector2.UnitX * GameWorld.PlayerMuzzleDistance;
                            var snapshot = new ActorPresentationState(PlayerAnimationState.Shoot, feet, muzzle, -1);

                            Assert(snapshot.AnimationState == PlayerAnimationState.Shoot, "snapshot preserves selected animation state");
                            Assert(snapshot.FeetAnchor == feet, "snapshot preserves the feet anchor");
                            Assert(snapshot.MuzzleAnchor == muzzle, "snapshot preserves the muzzle anchor");
                            Assert(snapshot.FacingDirection == -1, "snapshot preserves horizontal facing");
            });
        }
    }
}
