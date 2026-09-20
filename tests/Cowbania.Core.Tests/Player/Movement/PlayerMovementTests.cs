namespace Cowbania.Core.Tests.Player.Movement;

internal static class PlayerMovementTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("horizontal movement remains grounded and advances the player deterministically", () =>
            {
                var game = new GameWorld();

                            for (var i = 0; i < 12; i++)
                            {
                                game.Update(new InputFrame(1, false, false, Vector2.UnitX, false, false, false, false), 0.25f);
                            }

                            Assert(game.PlayerPosition.X > 80, "horizontal input advances the player");
                            Assert(game.PlayerPosition.Y == 480, "grounded movement stays on the shared ground plane");
            });
            yield return new TestCase("jump uses screen-space signs and lands on a raised platform", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(350, 480));
                            game.Update(new InputFrame(0, true, false, Vector2.UnitX, false, false, false, false), 0.05f);
                            Assert(game.PlayerVelocity.Y < 0, "jump velocity points upward as negative Y");
                            Assert(game.PlayerPosition.Y < 480, "jump lifts the feet above the ground");

                            for (var i = 0; i < 30; i++)
                                game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, false), 0.05f);

                            Assert(game.PlayerPosition.Y == 380, "falling player lands on the shared raised platform");
                            Assert(game.PlayerVelocity.Y == 0, "landing clears downward velocity");
            });
            yield return new TestCase("jump moves upward immediately in screen space", () =>
            {
                var game = new GameWorld();
                            var startingY = game.PlayerPosition.Y;
                            game.Update(new InputFrame(0, true, false, Vector2.UnitX, false, false, false, false), 0.016f);
                            Assert(game.PlayerPosition.Y < startingY, "jump immediately decreases screen-space Y");
                            Assert(game.PlayerVelocity.Y < 0, "jump starts with upward velocity");
                            Assert(game.LastJumpRequestOutcome == JumpRequestOutcome.Accepted,
                                "grounded jump records an accepted request outcome");
            });
            yield return new TestCase("jump diagnostics distinguish rejected requests", () =>
            {
                var airborne = new GameWorld();
                            SetProperty(airborne, nameof(GameWorld.PlayerPosition), new Vector2(500, 400));
                            airborne.Update(new InputFrame(0, true, false, Vector2.UnitX, false, false, false, false), 0.016f);
                            Assert(airborne.LastJumpRequestOutcome == JumpRequestOutcome.RejectedNotGrounded,
                                "unsupported jump records a not-grounded rejection");

                            var paused = new GameWorld();
                            paused.Update(new InputFrame(0, true, false, Vector2.UnitX, false, false, false, true), 0.016f);
                            Assert(paused.LastJumpRequestOutcome == JumpRequestOutcome.RejectedPaused,
                                "jump pressed while pausing records a paused rejection");
            });
            yield return new TestCase("falling below a canyon restores a safe authored spawn", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.PlayerPosition),
                                new Vector2(1300, RoomCatalog.Hub.Bounds.Bottom + GameWorld.PlayerBodyHeight + 1));
                            SetProperty(game, nameof(GameWorld.Health), GameWorld.MaximumHealth);

                            game.Update(default, 0f);

                            Assert(game.Health == GameWorld.MaximumHealth - 1,
                                "a missed canyon jump costs one health");
                            Assert(game.PlayerPosition == RoomCatalog.Hub.Spawn && game.PlayerVelocity == Vector2.Zero,
                                "fall recovery returns the player to a supported room spawn without residual velocity");
                            Assert(game.Projectiles.Count == 0,
                                "fall recovery clears transient projectiles before play resumes");
            });
            yield return new TestCase("gravity returns the player to a valid support surface", () =>
            {
                var game = new GameWorld();
                            game.Update(new InputFrame(0, true, false, Vector2.UnitX, false, false, false, false), 0.016f);
                            for (var i = 0; i < 120; i++)
                                game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, false), 0.016f);
                            Assert(game.PlayerPosition.Y == RoomCatalog.Hub.Ground.Y, "gravity returns the player to the hub ground");
                            Assert(game.PlayerVelocity.Y == 0, "landing clears vertical velocity");
            });
            yield return new TestCase("grounded state is derived from collision geometry", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(500, 380));
                            SetProperty(game, nameof(GameWorld.PlayerVelocity), Vector2.Zero);
                            game.Update(new InputFrame(0, true, false, Vector2.UnitX, false, false, false, false), 0.016f);
                            Assert(game.PlayerPosition.Y > 380, "jump input is ignored when the player is not supported");
                            Assert(game.PlayerVelocity.Y > 0, "unsupported player continues falling under gravity");
            });
            yield return new TestCase("player lands on a raised platform", () =>
            {
                var game = new GameWorld();
                            var platform = RoomCatalog.Hub.Solids[1];
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(platform.X + 40, platform.Y));
                            SetProperty(game, nameof(GameWorld.PlayerVelocity), Vector2.Zero);
                            game.Update(new InputFrame(0, true, false, Vector2.UnitX, false, false, false, false), 0.016f);
                            for (var i = 0; i < 120; i++)
                                game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, false), 0.016f);
                            Assert(game.PlayerPosition.Y == platform.Y, "descending player lands on the raised platform");
                            Assert(game.PlayerVelocity.Y == 0, "raised-platform landing clears vertical velocity");
            });
            yield return new TestCase("player feet remain exactly on the supporting surface when grounded", () =>
            {
                var game = new GameWorld();

                            Assert(game.PlayerPosition.Y == RoomCatalog.Hub.Ground.Y,
                                "initial player feet equal the hub ground top");

                            for (var i = 0; i < 120; i++)
                                game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, false), 0.016f);

                            Assert(game.PlayerPosition.Y == RoomCatalog.Hub.Ground.Y,
                                "settled player feet equal the hub ground top");

                            var platform = RoomCatalog.Hub.Solids[1];
                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(platform.X + 40, platform.Y));
                            SetProperty(game, nameof(GameWorld.PlayerVelocity), Vector2.Zero);
                            game.Update(new InputFrame(0, false, false, Vector2.UnitX, false, false, false, false), 0f);

                            Assert(game.PlayerPosition.Y == platform.Y,
                                "grounded player feet equal the raised platform top");
            });
            yield return new TestCase("horizontal movement updates facing and neutral fire direction", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));

                            game.Update(new InputFrame(1, false, false, Vector2.Zero, false, false, false, false), 0f);
                            Assert(game.FacingDirection == 1, "rightward movement faces right");
                            game.Update(new InputFrame(-1, false, false, Vector2.Zero, true, false, false, false), 0f);

                            Assert(game.FacingDirection == -1, "leftward movement faces left");
                            Assert(game.AimDirection == -Vector2.UnitX, "neutral aim follows the current facing");
                            Assert(game.Projectiles[0].Velocity == -Vector2.UnitX * 720f,
                                "neutral fire travels in the current facing direction");
            });
            yield return new TestCase("explicit vertical and diagonal aim override facing without losing horizontal facing", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));

                            game.Update(new InputFrame(-1, false, false, Vector2.Zero, false, false, false, false), 0f);
                            game.Update(new InputFrame(0, false, false, -Vector2.UnitY, true, false, false, false), 0f);
                            Assert(game.Projectiles[0].Velocity == -Vector2.UnitY * 720f,
                                "explicit vertical aim fires vertically");
                            Assert(game.FacingDirection == -1, "vertical aim preserves horizontal facing");

                            game.Update(new InputFrame(0, false, false, Vector2.Normalize(new Vector2(1, -1)), false, false, false, false), 0.2f);
                            Assert(game.FacingDirection == 1, "explicit diagonal aim updates horizontal facing");
                            Assert(Vector2.Distance(game.AimDirection, Vector2.Normalize(new Vector2(1, -1))) < 0.01f,
                                "explicit diagonal aim remains eight-way");
            });
            yield return new TestCase("movement advances by elapsed seconds rather than update-call count", () =>
            {
                var oneStep = new GameWorld();
                            var fourSteps = new GameWorld();
                            var input = new InputFrame(1, false, false, Vector2.Zero, false, false, false, false);

                            oneStep.Update(input, 0.4f);
                            for (var i = 0; i < 4; i++)
                                fourSteps.Update(input, 0.1f);

                            Assert(MathF.Abs(oneStep.PlayerPosition.X - fourSteps.PlayerPosition.X) < 0.01f,
                                "equal elapsed time produces equal horizontal displacement");
                            Assert(oneStep.PlayerPosition.Y == fourSteps.PlayerPosition.Y,
                                "equal elapsed time preserves the same support surface");
            });
            yield return new TestCase("dash duration is measured in elapsed seconds", () =>
            {
                var game = new GameWorld();
                            var input = new InputFrame(1, false, true, Vector2.Zero, false, false, false, false);
                            var start = game.PlayerPosition;

                            game.Update(input, 0.1f);
                            var duringDash = game.PlayerPosition.X - start.X;
                            game.Update(new InputFrame(1, false, false, Vector2.Zero, false, false, false, false), 0.1f);
                            var beforeExit = game.PlayerPosition.X;
                            game.Update(new InputFrame(1, false, false, Vector2.Zero, false, false, false, false), 0.02f);

                            Assert(duringDash > GameWorld.PlayerSpeed * 0.1f,
                                "dash moves faster than ordinary movement during its active window");
                            Assert(MathF.Abs(game.PlayerPosition.X - beforeExit - GameWorld.PlayerSpeed * 0.02f) < 0.01f,
                                "dash transitions to ordinary speed at its elapsed-time boundary");
            });
            yield return new TestCase("feet, facing, and muzzle contracts survive elapsed-time updates", () =>
            {
                var game = new GameWorld();
                            SetProperty(game, nameof(GameWorld.Enemy), new EnemyState(new Vector2(470, 480), 0, false));
                            var input = new InputFrame(-1, false, false, Vector2.Zero, false, false, false, false);

                            game.Update(input, 0.2f);
                            Assert(game.FacingDirection == -1, "horizontal movement updates facing");
                            Assert(game.PlayerPosition.Y == RoomCatalog.Hub.Ground.Y,
                                "horizontal movement keeps feet on the support surface");

                            SetProperty(game, nameof(GameWorld.PlayerPosition), new Vector2(300, RoomCatalog.Hub.Ground.Y));
                            game.Update(new InputFrame(0, false, false, Vector2.Zero, true, false, false, false), 0f);
                            Assert(game.Projectiles.Count == 1, "neutral fire still produces a projectile");
                            Assert(game.Projectiles[0].Position ==
                                game.PlayerPosition + GameWorld.PlayerMuzzleOffset -
                                Vector2.UnitX * GameWorld.PlayerMuzzleDistance,
                                "neutral fire preserves the muzzle offset and facing direction");
            });
        }
    }
}
