using Cowbania.Host.Diagnostics;
using Microsoft.Xna.Framework.Graphics;

namespace Cowbania.Host.Presentation.Assets;

internal sealed record FrontierAssets(
    IReadOnlyDictionary<string, Texture2D> Player,
    IReadOnlyDictionary<string, Texture2D> Bandit,
    IReadOnlyDictionary<string, Texture2D> Wildlife,
    IReadOnlyDictionary<string, Texture2D> Pickup,
    IReadOnlyDictionary<string, Texture2D> Terrain,
    IReadOnlyDictionary<string, Texture2D> Props,
    IReadOnlyDictionary<string, Texture2D> Effects,
    IReadOnlyDictionary<string, Texture2D> Ui,
    IReadOnlyDictionary<string, Texture2D> Backgrounds);

internal static class FrontierAssetLoader
{
    public static FrontierAssets Load(GraphicsDevice graphicsDevice)
    {
        var player = LoadActors(graphicsDevice, "Player", FrontierAnimationCatalog.PlayerClips.Values);
        StartupDiagnostics.Mark($"player sprites loaded ({player.Count})");
        var bandit = LoadActors(graphicsDevice, "Bandit", FrontierAnimationCatalog.BanditClips.Values);
        var wildlife = LoadActors(graphicsDevice, "Wildlife", FrontierAnimationCatalog.WildlifeClips.Values);
        StartupDiagnostics.Mark($"enemy sprites loaded (bandit={bandit.Count}, wildlife={wildlife.Count})");
        var pickup = LoadActors(graphicsDevice, "Pickup", FrontierAnimationCatalog.PickupClips.Values);
        StartupDiagnostics.Mark($"pickup sprites loaded ({pickup.Count})");

        var terrain = LoadNamed(graphicsDevice, "Terrain", 16, 16,
            "ground_cap", "ground_body", "platform_left", "platform_middle", "platform_right",
            "timber_support", "stone", "mine_reinforcement");
        var props = LoadNamed(graphicsDevice, "Props", 16, 16, "cactus_0", "cactus_1", "crate", "sign");
        AddNamed(graphicsDevice, props, "Props", 32, 32,
            "checkpoint", "shortcut", "transition_gate", "wagon_debris", "mine_timber");
        var effects = LoadNamed(graphicsDevice, "Effects", 16, 16,
            "muzzle_0", "muzzle_1", "muzzle_2", "impact_0", "impact_1", "impact_2",
            "dust_0", "dust_1", "dust_2", "dash_0", "dash_1", "dash_2",
            "hurt_0", "hurt_1", "defeat_0", "defeat_1", "defeat_2",
            "pickup_0", "pickup_1", "pickup_2", "pickup_3");
        var ui = LoadNamed(graphicsDevice, "UI", 16, 16,
            "heart_full", "heart_empty", "ammo_full", "ammo_empty", "currency", "slot_frame", "panel_corner");
        var backgrounds = LoadNamed(graphicsDevice, "Background", 256, 144,
            "hub_far", "hub_mid", "branch_far", "branch_mid");
        StartupDiagnostics.Mark(
            $"environment sprites loaded (terrain={terrain.Count}, props={props.Count}, " +
            $"effects={effects.Count}, ui={ui.Count}, backgrounds={backgrounds.Count})");
        return new FrontierAssets(player, bandit, wildlife, pickup, terrain, props, effects, ui, backgrounds);
    }

    private static Dictionary<string, Texture2D> LoadActors(
        GraphicsDevice graphicsDevice,
        string category,
        IEnumerable<AnimationClip> clips)
    {
        var cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        foreach (var key in clips.SelectMany(clip => clip.Frames).Select(frame => frame.AssetKey).Distinct(StringComparer.Ordinal))
            cache[key] = LoadSprite(graphicsDevice, key, 16, 16, category);
        return cache;
    }

    private static Dictionary<string, Texture2D> LoadNamed(
        GraphicsDevice graphicsDevice,
        string category,
        int width,
        int height,
        params string[] names)
    {
        var cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        AddNamed(graphicsDevice, cache, category, width, height, names);
        return cache;
    }

    private static void AddNamed(
        GraphicsDevice graphicsDevice,
        Dictionary<string, Texture2D> cache,
        string category,
        int width,
        int height,
        params string[] names)
    {
        foreach (var name in names)
            cache[name] = LoadSprite(graphicsDevice, $"Frontier/{category}/{name}.png", width, height, category);
    }

    private static Texture2D LoadSprite(
        GraphicsDevice graphicsDevice,
        string assetKey,
        int expectedWidth,
        int expectedHeight,
        string category)
    {
        var relativePath = Path.Combine("Assets", "Art", assetKey.Replace('/', Path.DirectorySeparatorChar));
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, relativePath),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath))
        };
        foreach (var path in candidates)
        {
            if (!File.Exists(path))
                continue;
            try
            {
                var texture = Texture2D.FromFile(graphicsDevice, path);
                if (texture.Width == expectedWidth && texture.Height == expectedHeight)
                    return texture;
                var actualWidth = texture.Width;
                var actualHeight = texture.Height;
                texture.Dispose();
                throw new InvalidDataException(
                    $"Frontier {category} asset '{relativePath}' has invalid dimensions. " +
                    $"Expected {expectedWidth}x{expectedHeight}, found {actualWidth}x{actualHeight}.");
            }
            catch (Exception exception) when (exception is not InvalidDataException)
            {
                throw new InvalidDataException(
                    $"Frontier {category} asset '{relativePath}' could not be decoded as a valid PNG.",
                    exception);
            }
        }
        throw new FileNotFoundException(
            $"Required Frontier {category} asset '{relativePath}' was not found. " +
            $"Checked packaged path '{candidates[0]}' and repository path '{candidates[1]}'.",
            candidates[0]);
    }
}
