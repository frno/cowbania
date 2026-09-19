namespace Cowbania.Host.Tests.Presentation.Rendering;

internal static class PresentationRenderingTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("terrain tiles keep integer scale and clip partial solid edges", () =>
            {
                var solid = new Microsoft.Xna.Framework.Rectangle(250, 380, 220, 24);
                            var tiles = TerrainTileLayout.Cover(solid).ToArray();

                            Assert(tiles.Length > 0, "raised terrain must produce tile draws");
                            Assert(tiles.All(tile =>
                                    tile.Destination.Width == tile.Source.Width * 3 &&
                                    tile.Destination.Height == tile.Source.Height * 3),
                                "every terrain source pixel must render at the same integer scale");
                            Assert(tiles.All(tile =>
                                    tile.Destination.Width == 48 &&
                                    tile.Destination.Height == 48),
                                "partial surface dimensions must not rescale individual tiles");
                            Assert(tiles.Max(tile => tile.Destination.Right) >= solid.Right &&
                                   tiles.Max(tile => tile.Destination.Bottom) >= solid.Bottom,
                                "full-size edge tiles must cover the exact solid for destination clipping");

                            var source = ReadSource(FindRepositoryRoot(), "src", "Cowbania.Host", "Presentation", "Rendering", "StageRenderer.cs");
                            var drawSolid = MethodBody(source, "DrawSolid");
                            Assert(drawSolid.Contains("Rectangle.Intersect(rectangle, viewport)", StringComparison.Ordinal) &&
                                   drawSolid.Contains("context.GraphicsDevice.ScissorRectangle = clip", StringComparison.Ordinal) &&
                                   drawSolid.Contains("TerrainTileLayout.Cover(rectangle)", StringComparison.Ordinal),
                                "terrain drawing must clip full integer-scaled tiles to the RoomCatalog solid rectangle");
            });
            yield return new TestCase("projectiles have persistent visuals distinct from transient effects", () =>
            {
                var player = ProjectileVisualCatalog.For(ProjectileOwner.Player);
                            var hostile = ProjectileVisualCatalog.For(ProjectileOwner.Enemy);

                            Assert(player.Shape == ProjectileVisualShape.PlayerTracer &&
                                   hostile.Shape == ProjectileVisualShape.HostileBolt,
                                "player and hostile projectiles need distinct directional silhouettes");
                            Assert(player.PrimaryColor != hostile.PrimaryColor &&
                                   player.SecondaryColor != hostile.SecondaryColor &&
                                   (player.Length, player.Thickness) != (hostile.Length, hostile.Thickness),
                                "projectile ownership must remain readable by color and geometry");

                            var root = FindRepositoryRoot();
                            var draw = MethodBody(ReadSource(root, "src", "Cowbania.Host", "Presentation", "Rendering", "GameRenderer.cs"), "Draw");
                            var projectile = MethodBody(ReadSource(root, "src", "Cowbania.Host", "Presentation", "Rendering", "ProjectileRenderer.cs"), "Draw");
                            Assert(draw.Contains("projectiles.Draw(projectile)", StringComparison.Ordinal),
                                "persistent projectile snapshots must use the dedicated projectile renderer");
                            Assert(!projectile.Contains("DrawEffect", StringComparison.Ordinal) &&
                                   !projectile.Contains("\"muzzle\"", StringComparison.Ordinal) &&
                                   !projectile.Contains("\"impact\"", StringComparison.Ordinal),
                                "muzzle and impact sprites must remain transient effects, never projectile bodies");
            });
            yield return new TestCase("Frontier renderer preserves sampling geometry depth and snapshot contracts", () =>
            {
                var root = FindRepositoryRoot();
                            var renderContext = ReadSource(root, "src", "Cowbania.Host", "Presentation", "Rendering", "RenderContext.cs");
                            var renderer = ReadSource(root, "src", "Cowbania.Host", "Presentation", "Rendering", "GameRenderer.cs");
                            var stage = ReadSource(root, "src", "Cowbania.Host", "Presentation", "Rendering", "StageRenderer.cs");
                            var actors = ReadSource(root, "src", "Cowbania.Host", "Presentation", "Rendering", "ActorRenderer.cs");
                            var effects = ReadSource(root, "src", "Cowbania.Host", "Presentation", "Effects", "EffectRenderer.cs");
                            var source = string.Concat(renderContext, renderer, stage, actors, effects);
                            var draw = MethodBody(renderer, "Draw");
                            var solid = MethodBody(stage, "DrawSolid");

                            Assert(source.Contains("SamplerState.PointClamp", StringComparison.Ordinal),
                                "Frontier rendering must use PointClamp");
                            var fractionalScaleCalls = System.Text.RegularExpressions.Regex.Matches(
                                    source, @"(?:DrawActorSprite|DrawAnchoredSprite|DrawEffect|DrawProp)\s*\([^;]*,\s*\d+\.\d+f\s*\)",
                                    System.Text.RegularExpressions.RegexOptions.Singleline)
                                .Select(match => System.Text.RegularExpressions.Regex.Replace(match.Value, @"\s+", " "))
                                .ToArray();
                            Assert(fractionalScaleCalls.Length == 0,
                                $"Frontier sprites and effects must use integer scale factors. Found: {string.Join(" | ", fractionalScaleCalls)}");
                            Assert(source.Contains("EnemyPresentationStateSelector.Select(enemy)", StringComparison.Ordinal) &&
                                   source.Contains("FrontierAnimationCatalog.ForEnemy(enemy)", StringComparison.Ordinal),
                                "enemy presentation must be selected from deterministic enemy snapshots");
                            Assert(!source.Contains("new Random", StringComparison.Ordinal) &&
                                   !source.Contains("DateTime.", StringComparison.Ordinal),
                                "presentation must not introduce nondeterministic random or wall-clock selection");
                            Assert(draw.Contains("foreach (var solid in room.Solids)", StringComparison.Ordinal) &&
                                   solid.Contains("context.Camera.ToScreen(solid)", StringComparison.Ordinal),
                                "rendered terrain must consume RoomCatalog solids directly");

                            AssertInOrder(draw,
                                "stage.DrawBackdrop", "stage.DrawSetDressing", "stage.DrawSolid", "stage.DrawLandmarks",
                                "actors.DrawPlayer", "world.Projectiles", "actors.DrawPickups",
                                "stage.DrawForeground", "hud.Draw");
            });
        }
    }
}
