namespace Cowbania.Host.Tests.Assets;

internal static class AssetsTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("Frontier runtime has no placeholder asset references", () =>
            {
                var root = FindRepositoryRoot();
                            var hostSource = string.Concat(Directory.GetFiles(
                                    Path.Combine(root, "src", "Cowbania.Host"), "*.cs", SearchOption.AllDirectories)
                                .Select(File.ReadAllText));
                            var hostProject = ReadSource(root, "src", "Cowbania.Host", "Cowbania.Host.csproj");

                            Assert(!hostSource.Contains("Placeholders", StringComparison.OrdinalIgnoreCase),
                                "the Host runtime must not reference the retired Placeholders asset tree");
                            Assert(!hostProject.Contains("Placeholders", StringComparison.OrdinalIgnoreCase),
                                "the Host project must not copy retired placeholder art");
                            Assert(AllAnimationAssetKeys().All(key => key.StartsWith("Frontier/", StringComparison.Ordinal)),
                                "every runtime animation key resolves inside the Frontier pack");
            });
            yield return new TestCase("Frontier manifest and PNG inventory are complete", () =>
            {
                var root = FindRepositoryRoot();
                            var frontierRoot = Path.Combine(root, "Assets", "Art", "Frontier");
                            var expected = ExpectedFrontierAssets();
                            var actual = Directory.GetFiles(frontierRoot, "*.png", SearchOption.AllDirectories)
                                .Select(path => Path.GetRelativePath(Path.Combine(root, "Assets", "Art"), path).Replace('\\', '/'))
                                .ToHashSet(StringComparer.Ordinal);

                            Assert(expected.SetEquals(actual),
                                $"Frontier PNG inventory differs. Missing=[{string.Join(", ", expected.Except(actual).Order())}] " +
                                $"Unexpected=[{string.Join(", ", actual.Except(expected).Order())}]");
                            Assert(File.Exists(Path.Combine(frontierRoot, "manifest.md")),
                                "the authored Frontier manifest must ship with the source pack");
            });
            yield return new TestCase("Frontier PNGs have valid RGBA signatures dimensions and transparency", () =>
            {
                var artRoot = Path.Combine(FindRepositoryRoot(), "Assets", "Art");
                            foreach (var asset in ExpectedFrontierAssets())
                            {
                                var path = Path.Combine(artRoot, asset.Replace('/', Path.DirectorySeparatorChar));
                                var png = ReadPng(path);
                                var expectedSize = ExpectedPngSize(asset);

                                Assert((png.Width, png.Height) == expectedSize,
                                    $"{asset} must be {expectedSize.Width}x{expectedSize.Height}, found {png.Width}x{png.Height}");
                                Assert(png.BitDepth == 8 && png.ColorType == 6 && png.InterlaceMethod == 0,
                                    $"{asset} must be a non-interlaced 8-bit RGBA PNG");
                                Assert(png.MaximumAlpha == 255, $"{asset} must contain visible opaque artwork");
                                var opaqueFillTile =
                                    asset.EndsWith("/Terrain/ground_body.png", StringComparison.Ordinal) ||
                                    asset.EndsWith("/Terrain/stone.png", StringComparison.Ordinal);
                                Assert(opaqueFillTile || png.MinimumAlpha == 0,
                                    $"{asset} must contain transparent background pixels");
                            }
            });
            yield return new TestCase("Frontier actor and pickup mappings are distinct", () =>
            {
                var bandit = FrontierAnimationCatalog.BanditClips.Values
                                .SelectMany(clip => clip.Frames).Select(frame => frame.AssetKey).ToHashSet(StringComparer.Ordinal);
                            var wildlife = FrontierAnimationCatalog.WildlifeClips.Values
                                .SelectMany(clip => clip.Frames).Select(frame => frame.AssetKey).ToHashSet(StringComparer.Ordinal);
                            Assert(!bandit.Overlaps(wildlife), "bandit and wildlife must not share sprite mappings");
                            Assert(bandit.All(key => key.StartsWith("Frontier/Bandit/", StringComparison.Ordinal)),
                                "bandit clips must resolve to Bandit art");
                            Assert(wildlife.All(key => key.StartsWith("Frontier/Wildlife/", StringComparison.Ordinal)),
                                "wildlife clips must resolve to Wildlife art");

                            var pickupSets = Enum.GetValues<PickupType>().ToDictionary(
                                type => type,
                                type => FrontierAnimationCatalog.ForPickup(type).Frames
                                    .Select(frame => frame.AssetKey).ToHashSet(StringComparer.Ordinal));
                            foreach (var left in pickupSets)
                            foreach (var right in pickupSets)
                                if (left.Key < right.Key)
                                    Assert(!left.Value.Overlaps(right.Value),
                                        $"{left.Key} and {right.Key} pickups must use distinct icon frames");
            });
            yield return new TestCase("Frontier output copy and explicit missing asset behavior are enforced", () =>
            {
                var root = FindRepositoryRoot();
                            var project = ReadSource(root, "src", "Cowbania.Host", "Cowbania.Host.csproj");
                            Assert(project.Contains(@"Assets\Art\Frontier\**\*.png", StringComparison.Ordinal) &&
                                   project.Contains("CopyToOutputDirectory=\"PreserveNewest\"", StringComparison.Ordinal) &&
                                   project.Contains("%(RecursiveDir)", StringComparison.Ordinal),
                                "the Host project must recursively preserve the Frontier asset tree in build output");

                            var outputArt = Path.Combine(root, "src", "Cowbania.Host", "bin", "Debug", "net10.0", "Assets", "Art");
                            foreach (var asset in ExpectedFrontierAssets())
                                Assert(File.Exists(Path.Combine(outputArt, asset.Replace('/', Path.DirectorySeparatorChar))),
                                    $"required output asset is missing: {asset}");

                            var loader = MethodBody(ReadSource(root, "src", "Cowbania.Host", "Presentation", "Assets", "FrontierAssets.cs"), "LoadSprite");
                            Assert(loader.Contains("throw new FileNotFoundException", StringComparison.Ordinal) &&
                                   loader.Contains("Required Frontier", StringComparison.Ordinal) &&
                                   loader.Contains("relativePath", StringComparison.Ordinal) &&
                                   !loader.Contains("Placeholders", StringComparison.OrdinalIgnoreCase),
                                "missing Frontier art must fail with the exact relative asset path and no placeholder fallback");
            });
        }
    }
}
