using System.Collections.Immutable;
using System.Numerics;
using Cowbania.Core.Gameplay;
using Cowbania.Core.Gameplay.Enemies;
using Cowbania.Core.Gameplay.Pickups;
using Cowbania.Core.Presentation.Enemies;

namespace Cowbania.Core.Presentation.Animation;

public static class FrontierAnimationCatalog
{
    public static AnimationActorMetadata PlayerMetadata { get; } =
        new(new Vector2(16, 27), new Vector2(25, 15));

    public static AnimationActorMetadata BanditMetadata { get; } =
        new(new Vector2(8, 13), new Vector2(14, 7));

    public static AnimationActorMetadata WildlifeMetadata { get; } =
        new(new Vector2(8, 13), new Vector2(14, 9));

    public static AnimationActorMetadata PickupMetadata { get; } =
        new(new Vector2(8, 13), new Vector2(8, 8));

    public static ImmutableDictionary<PresentationAnimationState, AnimationClip> PlayerClips { get; } =
        new Dictionary<PresentationAnimationState, AnimationClip>
        {
            [PresentationAnimationState.Idle] = Clip("player_idle", "Frontier/Player", "idle", 4, 6f),
            [PresentationAnimationState.Run] = Clip("player_run", "Frontier/Player", "run", 6, 12f),
            [PresentationAnimationState.Jump] = Clip("player_jump", "Frontier/Player", "jump", 2, 8f),
            [PresentationAnimationState.Fall] = Clip("player_fall", "Frontier/Player", "fall", 2, 8f),
            [PresentationAnimationState.Shoot] = Clip(
                "player_shoot", "Frontier/Player", "shoot", 3, 15f, AnimationPlaybackMode.OneShot),
            [PresentationAnimationState.Reload] = Clip(
                "player_reload", "Frontier/Player", "reload", 4, 4f / GameWorld.ReloadDuration,
                AnimationPlaybackMode.OneShot),
            [PresentationAnimationState.Hurt] = Clip(
                "player_hurt", "Frontier/Player", "hurt", 2, 10f, AnimationPlaybackMode.OneShot),
            [PresentationAnimationState.Dash] = Clip(
                "player_dash", "Frontier/Player", "dash", 3, 15f, AnimationPlaybackMode.OneShot)
        }.ToImmutableDictionary();

    public static ImmutableDictionary<PresentationAnimationState, AnimationClip> BanditClips { get; } =
        new Dictionary<PresentationAnimationState, AnimationClip>
        {
            [PresentationAnimationState.BanditPatrol] =
                Clip("bandit_patrol", "Frontier/Bandit", "patrol", 4, 6f),
            [PresentationAnimationState.BanditNotice] =
                Clip("bandit_notice", "Frontier/Bandit", "notice", 2, 8f, AnimationPlaybackMode.OneShot),
            [PresentationAnimationState.BanditAttack] =
                Clip("bandit_attack", "Frontier/Bandit", "attack", 4, 12f, AnimationPlaybackMode.OneShot),
            [PresentationAnimationState.EnemyDefeated] =
                Clip("bandit_defeated", "Frontier/Bandit", "defeated", 2, 6f, AnimationPlaybackMode.OneShot)
        }.ToImmutableDictionary();

    public static ImmutableDictionary<PresentationAnimationState, AnimationClip> WildlifeClips { get; } =
        new Dictionary<PresentationAnimationState, AnimationClip>
        {
            [PresentationAnimationState.WildlifePatrol] =
                Clip("wildlife_patrol", "Frontier/Wildlife", "patrol", 4, 8f),
            [PresentationAnimationState.WildlifeNotice] =
                Clip("wildlife_notice", "Frontier/Wildlife", "notice", 2, 8f, AnimationPlaybackMode.OneShot),
            [PresentationAnimationState.WildlifeLunge] =
                Clip("wildlife_lunge", "Frontier/Wildlife", "lunge", 4, 12f, AnimationPlaybackMode.OneShot),
            [PresentationAnimationState.EnemyDefeated] =
                Clip("wildlife_defeated", "Frontier/Wildlife", "defeated", 2, 6f, AnimationPlaybackMode.OneShot)
        }.ToImmutableDictionary();

    public static ImmutableDictionary<PickupType, AnimationClip> PickupClips { get; } =
        new Dictionary<PickupType, AnimationClip>
        {
            [PickupType.Currency] =
                Clip("currency_float", "Frontier/Pickup/Currency", "float", 4, 6f),
            [PickupType.Health] =
                Clip("health_float", "Frontier/Pickup/Health", "float", 4, 6f),
            [PickupType.ReserveAmmo] =
                Clip("ammo_float", "Frontier/Pickup/Ammo", "float", 4, 6f)
        }.ToImmutableDictionary();

    public static AnimationClip For(PresentationAnimationState state) => state switch
    {
        PresentationAnimationState.EnemyIdle => BanditClips[PresentationAnimationState.BanditPatrol],
        PresentationAnimationState.PickupFloat => PickupClips[PickupType.Currency],
        PresentationAnimationState.BanditPatrol or
        PresentationAnimationState.BanditNotice or
        PresentationAnimationState.BanditAttack or
        PresentationAnimationState.EnemyDefeated => BanditClips[state],
        PresentationAnimationState.WildlifePatrol or
        PresentationAnimationState.WildlifeNotice or
        PresentationAnimationState.WildlifeLunge => WildlifeClips[state],
        _ => PlayerClips[state]
    };

    public static AnimationClip ForEnemy(EnemyState enemy)
    {
        var state = EnemyPresentationStateSelector.Select(enemy).AnimationState;
        return enemy.Archetype == EnemyArchetype.Bandit
            ? BanditClips[state]
            : WildlifeClips[state];
    }

    public static AnimationClip ForPickup(PickupType type) => PickupClips[type];

    private static AnimationClip Clip(
        string name,
        string path,
        string frameName,
        int frameCount,
        float framesPerSecond,
        AnimationPlaybackMode playbackMode = AnimationPlaybackMode.Loop) =>
        new(
            name,
            Enumerable.Range(0, frameCount)
                .Select(index => new AnimationFrame($"{path}/{frameName}_{index}.png")),
            framesPerSecond,
            playbackMode);
}
