namespace Cowbania.Host.Tests.Presentation.Hud;

internal static class PresentationHudTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("HUD and combat telegraphs include non-color identity cues", () =>
            {
                var root = FindRepositoryRoot();
                            var hudSource = ReadSource(root, "src", "Cowbania.Host", "Presentation", "Hud", "HudRenderer.cs");
                            var hud = MethodBody(hudSource, "Draw");
                            var scoreHud = MethodBody(hudSource, "DrawScore");
                            foreach (var icon in new[] { "heart_full", "heart_empty", "ammo_full", "ammo_empty", "slot_frame" })
                                Assert(hud.Contains($"\"{icon}\"", StringComparison.Ordinal), $"HUD must use the {icon} Frontier icon");
                            Assert(scoreHud.Contains("\"coin\"", StringComparison.Ordinal),
                                "the score HUD must use the coin Frontier icon");
                            Assert(hud.Contains("DrawScore(world, width)", StringComparison.Ordinal) &&
                                   scoreHud.Contains("viewportWidth / 2 - 140", StringComparison.Ordinal) &&
                                   scoreHud.Contains("world.Score", StringComparison.Ordinal) &&
                                   scoreHud.Contains("world.CurrentRoomCoinsCollected", StringComparison.Ordinal) &&
                                   scoreHud.Contains("world.CurrentRoomTotalCoins", StringComparison.Ordinal) &&
                                   !scoreHud.Contains("Number(world.TotalCoins", StringComparison.Ordinal),
                                "the centered top HUD must show global score plus current-room coin progress");

                            var bandit = EnemyPresentationStateSelector.Select(new EnemyState(
                                "bandit", EnemyArchetype.Bandit, EnemyBehaviorState.Attack, EnemyAttackPhase.Telegraph,
                                default, default, 1, 2, true, 0, 0.5f));
                            var wildlife = EnemyPresentationStateSelector.Select(new EnemyState(
                                "wildlife", EnemyArchetype.Wildlife, EnemyBehaviorState.Attack, EnemyAttackPhase.Telegraph,
                                default, default, 1, 2, true, 0, 0.5f));
                            Assert(bandit.TelegraphMarker == EnemyTelegraphMarker.BanditQuickDrawWarning &&
                                   wildlife.TelegraphMarker == EnemyTelegraphMarker.WildlifeLungeArrow &&
                                   bandit.AnimationState != wildlife.AnimationState,
                                "bandit and wildlife telegraphs must differ by geometry and pose, not tint alone");
                            var telegraph = MethodBody(
                                ReadSource(root, "src", "Cowbania.Host", "Presentation", "Rendering", "ActorRenderer.cs"),
                                "DrawTelegraph");
                            Assert(telegraph.Contains("BanditQuickDrawWarning", StringComparison.Ordinal) &&
                                   telegraph.Contains("WildlifeLungeArrow", StringComparison.Ordinal) &&
                                   telegraph.Contains("Rectangle", StringComparison.Ordinal),
                                "telegraph rendering must provide persistent shape cues in addition to color");
                            Assert(!telegraph.Contains("aimLength", StringComparison.Ordinal) &&
                                   telegraph.Contains("warningMuzzleX", StringComparison.Ordinal) &&
                                   telegraph.Contains("pulse", StringComparison.Ordinal),
                                "bandit warning stays local to the gun instead of drawing a laser-like aim line");
            });
        }
    }
}
