namespace Cowbania.Host.Tests.Harness;

internal static class AssetInventory
{
    public static IEnumerable<string> AllAnimationAssetKeys() =>
        FrontierAnimationCatalog.PlayerClips.Values
            .Concat(FrontierAnimationCatalog.BanditClips.Values)
            .Concat(FrontierAnimationCatalog.WildlifeClips.Values)
            .Concat(FrontierAnimationCatalog.PickupClips.Values)
            .SelectMany(clip => clip.Frames)
            .Select(frame => frame.AssetKey)
            .Distinct(StringComparer.Ordinal);

    public static HashSet<string> ExpectedFrontierAssets()
    {
        var assets = AllAnimationAssetKeys().ToHashSet(StringComparer.Ordinal);
        AddNamed(assets, "Terrain", "ground_cap", "ground_body", "platform_left", "platform_middle",
            "platform_right", "timber_support", "stone", "mine_reinforcement");
        AddNamed(assets, "Props", "cactus_0", "cactus_1", "crate", "sign", "checkpoint", "shortcut",
            "transition_gate", "wagon_debris", "mine_timber");
        AddNamed(assets, "Effects", "muzzle_0", "muzzle_1", "muzzle_2", "impact_0", "impact_1", "impact_2",
            "dust_0", "dust_1", "dust_2", "dash_0", "dash_1", "dash_2", "hurt_0", "hurt_1",
            "defeat_0", "defeat_1", "defeat_2", "pickup_0", "pickup_1", "pickup_2", "pickup_3");
        AddNamed(assets, "UI", "heart_full", "heart_empty", "ammo_full", "ammo_empty", "currency",
            "slot_frame", "panel_corner");
        AddNamed(assets, "Background", "hub_far", "hub_mid", "branch_far", "branch_mid");
        return assets;
    }

    public static (int Width, int Height) ExpectedPngSize(string asset)
    {
        if (asset.StartsWith("Frontier/Background/", StringComparison.Ordinal))
            return (256, 144);
        if (new[] { "checkpoint.png", "shortcut.png", "transition_gate.png", "wagon_debris.png", "mine_timber.png" }
            .Any(name => asset.EndsWith($"/{name}", StringComparison.Ordinal)))
            return (32, 32);
        return (16, 16);
    }

    private static void AddNamed(HashSet<string> assets, string category, params string[] names)
    {
        foreach (var name in names)
            assets.Add($"Frontier/{category}/{name}.png");
    }
}
