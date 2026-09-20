namespace Cowbania.Core.Tests.Animation;

internal static class AnimationTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("animation clocks loop and complete one-shot clips deterministically", () =>
            {
                var loop = new AnimationClip("loop", new[] { new AnimationFrame("a"), new AnimationFrame("b") }, 2);
                            var clock = new AnimationClock();
                            clock.Advance(0.5f, loop);
                            Assert(clock.CurrentFrame(loop).AssetKey == "b", "loop advances at the frame boundary");
                            clock.Advance(0.5f, loop);
                            Assert(clock.CurrentFrame(loop).AssetKey == "a" && !clock.IsComplete, "loop wraps without completing");

                            var oneShot = loop with { PlaybackMode = AnimationPlaybackMode.OneShot };
                            clock.Reset();
                            clock.Advance(0.5f, oneShot);
                            Assert(clock.CurrentFrame(oneShot).AssetKey == "b" && !clock.IsComplete,
                                "one-shot remains active while showing its final frame");
                            clock.Advance(0.5f, oneShot);
                            Assert(clock.IsComplete && clock.CurrentFrame(oneShot).AssetKey == "b",
                                "one-shot completes after the final frame duration");
            });
            yield return new TestCase("enemy presentation clocks reset when the selected state changes", () =>
            {
                var clock = new PresentationAnimationClock();
                            clock.Advance(0.25f, PresentationAnimationState.BanditPatrol);
                            Assert(clock.CurrentFrameIndex == 1, "bandit patrol advances deterministically");

                            clock.Advance(0f, PresentationAnimationState.BanditNotice);
                            Assert(clock.CurrentState == PresentationAnimationState.BanditNotice &&
                                   clock.CurrentFrameIndex == 0 &&
                                   clock.CurrentFrame().AssetKey == "Frontier/Bandit/notice_0.png",
                                "changing enemy state resets the clock to the authored notice pose");
            });
            yield return new TestCase("looping animation clips advance deterministically and wrap", () =>
            {
                var clip = new AnimationClip(
                                "idle",
                                new[]
                                {
                                    new AnimationFrame("idle_0"),
                                    new AnimationFrame("idle_1"),
                                    new AnimationFrame("idle_2")
                                },
                                10f,
                                AnimationPlaybackMode.Loop);
                            var clock = new AnimationClock();

                            clock.Advance(0.25f, clip);

                            Assert(clock.CurrentFrameIndex == 2, "quarter-second idle advance reaches the third frame");
                            Assert(clock.CurrentFrame(clip).AssetKey == "idle_2", "current idle frame matches the clip key");
                            Assert(!clock.IsComplete, "looping clips never complete");

                            clock.Advance(0.1f, clip);
                            Assert(clock.CurrentFrameIndex == 0, "looping clips wrap to their first frame");
            });
            yield return new TestCase("one-shot animation clips clamp on completion", () =>
            {
                var clip = new AnimationClip(
                                "shoot",
                                new[] { new AnimationFrame("shoot_0"), new AnimationFrame("shoot_1") },
                                10f,
                                AnimationPlaybackMode.OneShot);
                            var clock = new AnimationClock();

                            clock.Advance(0.1f, clip);
                            Assert(clock.CurrentFrameIndex == 1, "one-shot advance reaches its final frame");
                            Assert(!clock.IsComplete, "one-shot remains visible on its final frame for its duration");
                            clock.Advance(0.1f, clip);
                            Assert(clock.IsComplete, "one-shot clip reports completion at its final frame");

                            clock.Advance(1f, clip);
                            Assert(clock.CurrentFrameIndex == 1, "completed one-shot clips remain clamped");
                            Assert(clock.CurrentFrame(clip).AssetKey == "shoot_1", "completed clip keeps its final asset");

                            clock.Reset();
                            Assert(clock.CurrentFrameIndex == 0 && clock.ElapsedSeconds == 0f && !clock.IsComplete,
                                "reset makes a completed one-shot reusable");
            });
            yield return new TestCase("animation clocks advance by elapsed seconds including large deltas", () =>
            {
                var clip = new AnimationClip(
                                "run",
                                new[] { new AnimationFrame("run_0"), new AnimationFrame("run_1") },
                                4f,
                                AnimationPlaybackMode.Loop);
                            var clock = new AnimationClock();

                            clock.Advance(1.125f, clip);

                            Assert(clock.CurrentFrameIndex == 0, "a full loop plus remainder returns to the first frame");
                            Assert(MathF.Abs(clock.ElapsedSeconds - 0.125f) < 0.0001f,
                                "large elapsed deltas preserve the fractional frame remainder");
            });
            yield return new TestCase("frontier player animation states preserve the stable contract", () =>
            {
                var expected = new[]
                            {
                                PlayerAnimationState.Idle,
                                PlayerAnimationState.Run,
                                PlayerAnimationState.Jump,
                                PlayerAnimationState.Fall,
                                PlayerAnimationState.Shoot,
                                PlayerAnimationState.Reload,
                                PlayerAnimationState.Hurt,
                                PlayerAnimationState.Dash
                            };

                            Assert(Enum.GetValues<PlayerAnimationState>().SequenceEqual(expected),
                                "player states contain idle, run, jump, fall, shoot, reload, hurt, and dash in contract order");
            });
            yield return new TestCase("frontier player clips map every authored frame and timing", () =>
            {
                var expected = new[]
                            {
                                (PresentationAnimationState.Idle, "player_idle", "idle", 4, 6f, AnimationPlaybackMode.Loop),
                                (PresentationAnimationState.Run, "player_run", "run", 6, 12f, AnimationPlaybackMode.Loop),
                                (PresentationAnimationState.Jump, "player_jump", "jump", 2, 8f, AnimationPlaybackMode.Loop),
                                (PresentationAnimationState.Fall, "player_fall", "fall", 2, 8f, AnimationPlaybackMode.Loop),
                                (PresentationAnimationState.Shoot, "player_shoot", "shoot", 3, 15f, AnimationPlaybackMode.OneShot),
                                (PresentationAnimationState.Reload, "player_reload", "reload", 4, 4f / GameWorld.ReloadDuration, AnimationPlaybackMode.OneShot),
                                (PresentationAnimationState.Hurt, "player_hurt", "hurt", 2, 10f, AnimationPlaybackMode.OneShot),
                                (PresentationAnimationState.Dash, "player_dash", "dash", 3, 15f, AnimationPlaybackMode.OneShot)
                            };

                            Assert(FrontierAnimationCatalog.PlayerClips.Count == expected.Length,
                                "the player catalog contains exactly the eight stable states");
                            foreach (var (state, name, frameName, count, fps, mode) in expected)
                            {
                                var clip = FrontierAnimationCatalog.For(state);
                                Assert(clip.Name == name, $"{state} retains its stable clip name");
                                Assert(clip.Frames.Length == count, $"{state} has its authored frame count");
                                Assert(MathF.Abs(clip.FramesPerSecond - fps) < 0.0001f, $"{state} has its authored timing");
                                Assert(clip.PlaybackMode == mode, $"{state} has its authored playback mode");
                                Assert(clip.Frames.Select(frame => frame.AssetKey).SequenceEqual(
                                        Enumerable.Range(0, count)
                                            .Select(index => $"Frontier/Player/{frameName}_{index}.png")),
                                    $"{state} maps every exact Frontier player asset");
                            }

                            var reload = FrontierAnimationCatalog.For(PresentationAnimationState.Reload);
                            Assert(MathF.Abs(reload.FrameDuration * reload.Frames.Length - GameWorld.ReloadDuration) < 0.0001f,
                                "reload clip duration exactly matches the authoritative gameplay reload duration");
            });
            yield return new TestCase("frontier actor metadata preserves feet and effect anchors", () =>
            {
                Assert(FrontierAnimationCatalog.PlayerMetadata.SourceFeetAnchor == new Vector2(16, 27),
                                "player sprites preserve the required source feet anchor");
                            Assert(FrontierAnimationCatalog.BanditMetadata.SourceFeetAnchor == new Vector2(8, 13) &&
                                   FrontierAnimationCatalog.WildlifeMetadata.SourceFeetAnchor == new Vector2(8, 13) &&
                                   FrontierAnimationCatalog.PickupMetadata.SourceFeetAnchor == new Vector2(8, 13),
                                "all Frontier actors expose the shared source feet anchor");
                            Assert(FrontierAnimationCatalog.PlayerMetadata.SourceEffectAnchor == new Vector2(25, 15),
                                "player metadata exposes the authored source muzzle anchor");
                            Assert(FrontierAnimationCatalog.BanditMetadata.SourceEffectAnchor == new Vector2(14, 7),
                                "bandit metadata exposes its authored muzzle anchor");
                            Assert(FrontierAnimationCatalog.WildlifeMetadata.SourceEffectAnchor == new Vector2(14, 9),
                                "wildlife metadata exposes its authored lunge effect anchor");
                            Assert(FrontierAnimationCatalog.PickupMetadata.SourceEffectAnchor == new Vector2(8, 8),
                                "pickup metadata exposes its authored center effect anchor");
            });
            yield return new TestCase("frontier enemy clips map both archetypes without inference", () =>
            {
                AssertEnemyClips(
                                FrontierAnimationCatalog.BanditClips,
                                "Bandit",
                                new[]
                                {
                                    (PresentationAnimationState.BanditPatrol, "patrol", 4, 6f, AnimationPlaybackMode.Loop),
                                    (PresentationAnimationState.BanditNotice, "notice", 2, 8f, AnimationPlaybackMode.OneShot),
                                    (PresentationAnimationState.BanditAttack, "attack", 4, 12f, AnimationPlaybackMode.OneShot),
                                    (PresentationAnimationState.EnemyDefeated, "defeated", 2, 6f, AnimationPlaybackMode.OneShot)
                                });
                            AssertEnemyClips(
                                FrontierAnimationCatalog.WildlifeClips,
                                "Wildlife",
                                new[]
                                {
                                    (PresentationAnimationState.WildlifePatrol, "patrol", 4, 8f, AnimationPlaybackMode.Loop),
                                    (PresentationAnimationState.WildlifeNotice, "notice", 2, 8f, AnimationPlaybackMode.OneShot),
                                    (PresentationAnimationState.WildlifeLunge, "lunge", 4, 12f, AnimationPlaybackMode.OneShot),
                                    (PresentationAnimationState.EnemyDefeated, "defeated", 2, 6f, AnimationPlaybackMode.OneShot)
                                });

                            var defeatedBandit = new EnemyState(
                                "bandit", EnemyArchetype.Bandit, EnemyBehaviorState.Defeated, EnemyAttackPhase.None,
                                Vector2.Zero, Vector2.Zero, 1, 0, false, 0, 0);
                            var defeatedWildlife = defeatedBandit with
                            {
                                Id = "wildlife",
                                Archetype = EnemyArchetype.Wildlife
                            };
                            Assert(FrontierAnimationCatalog.ForEnemy(defeatedBandit).Frames[0].AssetKey ==
                                   "Frontier/Bandit/defeated_0.png",
                                "defeated bandit selection remains archetype-specific");
                            Assert(FrontierAnimationCatalog.ForEnemy(defeatedWildlife).Frames[0].AssetKey ==
                                   "Frontier/Wildlife/defeated_0.png",
                                "defeated wildlife selection remains archetype-specific");

                            var clock = new PresentationAnimationClock();
                            clock.Advance(0, defeatedWildlife);
                            Assert(clock.CurrentClip == FrontierAnimationCatalog.WildlifeClips[PresentationAnimationState.EnemyDefeated] &&
                                   clock.CurrentFrame().AssetKey == "Frontier/Wildlife/defeated_0.png",
                                "snapshot-driven clocks retain the selected enemy archetype clip");
            });
            yield return new TestCase("frontier pickup clips cover currency health and ammo", () =>
            {
                var expected = new[]
                            {
                                (PickupType.Currency, "Currency"),
                                (PickupType.Health, "Health"),
                                (PickupType.ReserveAmmo, "Ammo")
                            };

                            Assert(FrontierAnimationCatalog.PickupClips.Count == expected.Length,
                                "the pickup catalog contains exactly the three gameplay pickup types");
                            foreach (var (type, folder) in expected)
                            {
                                var clip = FrontierAnimationCatalog.ForPickup(type);
                                Assert(clip.Frames.Length == 4 && clip.PlaybackMode == AnimationPlaybackMode.Loop,
                                    $"{type} uses a four-frame looping float clip");
                                Assert(MathF.Abs(clip.FrameDuration - 1f / 6f) < 0.0001f,
                                    $"{type} exposes the authored frame duration");
                                Assert(clip.Frames.Select(frame => frame.AssetKey).SequenceEqual(
                                        Enumerable.Range(0, 4)
                                            .Select(index => $"Frontier/Pickup/{folder}/float_{index}.png")),
                                    $"{type} maps every exact Frontier pickup asset");
                            }

                            var clock = new PresentationAnimationClock();
                            clock.Advance(0.2f, PickupType.Currency);
                            clock.Advance(0f, PickupType.Health);
                            Assert(clock.CurrentFrameIndex == 0 &&
                                   clock.CurrentFrame().AssetKey == "Frontier/Pickup/Health/float_0.png",
                                "changing pickup type resets to the correct snapshot-selected clip");
            });
            yield return new TestCase("frontier clocks freeze externally and equivalent progression matches", () =>
            {
                var clip = FrontierAnimationCatalog.For(PresentationAnimationState.Run);
                            var first = new AnimationClock();
                            var second = new AnimationClock();

                            foreach (var elapsed in new[] { 0.03f, 0f, 0.07f, 0.15f, 0.41f })
                            {
                                first.Advance(elapsed, clip);
                                second.Advance(elapsed, clip);
                            }

                            Assert(first.CurrentFrameIndex == second.CurrentFrameIndex &&
                                   first.ElapsedSeconds == second.ElapsedSeconds &&
                                   first.CurrentFrame(clip) == second.CurrentFrame(clip),
                                "equivalent clips and elapsed inputs produce equivalent frames");

                            var frozenFrame = first.CurrentFrameIndex;
                            var frozenElapsed = first.ElapsedSeconds;
                            first.Advance(0f, clip);
                            Assert(first.CurrentFrameIndex == frozenFrame && first.ElapsedSeconds == frozenElapsed,
                                "not advancing presentation time leaves the clock externally freezeable");

                            var oneShot = FrontierAnimationCatalog.For(PresentationAnimationState.Shoot);
                            var oneShotClock = new AnimationClock();
                            oneShotClock.Advance(oneShot.FrameDuration * oneShot.Frames.Length, oneShot);
                            Assert(oneShotClock.IsComplete &&
                                   oneShotClock.CurrentFrameIndex == oneShot.Frames.Length - 1,
                                "Frontier one-shots complete and clamp on their final authored frame");
            });
        }
    }
}
