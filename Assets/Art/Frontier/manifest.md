# Release 6 Frontier Visual Identity

Original, locally generated pixel art for Cowbania's Dust-Gothic Frontier presentation. Run `python tools\generate_frontier_assets.py` from this directory (or invoke it by absolute path) to reproduce and validate the complete PNG pack.

## Stable asset contract

| Group | Dimensions | Frames / files |
|---|---:|---|
| Player | 32x32 | `idle_0..3`, `run_0..5`, `jump_0..1`, `fall_0..1`, `shoot_0..2`, `reload_0..3`, `hurt_0..1`, `dash_0..2` |
| Bandit | 16x16 | `patrol_0..3`, `notice_0..1`, `attack_0..3`, `defeated_0..1` |
| Wildlife | 16x16 | `patrol_0..3`, `notice_0..1`, `lunge_0..3`, `defeated_0..1` |
| Pickups | 16x16 | Currency, Health, and Ammo `float_0..3` |
| Terrain | 16x16 | `ground_cap`, `ground_body`, `platform_left`, `platform_middle`, `platform_right`, `timber_support`, `stone`, `mine_reinforcement` |
| Props | 16x16 | `cactus_0`, `cactus_1`, `crate`, `sign` |
| Props | 32x32 | `checkpoint`, `shortcut`, `transition_gate`, `wagon_debris`, `mine_timber` |
| Effects | 16x16 | `muzzle_0..2`, `impact_0..2`, `dust_0..2`, `dash_0..2`, `hurt_0..1`, `defeat_0..2`, `pickup_0..3` |
| UI | 16x16 | `heart_full`, `heart_empty`, `ammo_full`, `ammo_empty`, `currency`, `slot_frame`, `panel_corner` |
| Background | 256x144 | `hub_far`, `hub_mid`, `branch_far`, `branch_mid` |

All PNGs are RGBA with transparent backgrounds. Backgrounds contain only scenery silhouettes, not an opaque sky, and may be tiled or stretched behind the authored room.

## Anchors and rendering

- Player source canvas: **32x32**. Feet anchor: source pixel **(16,27)**. Rows 28–31 remain transparent, and animation changes do not translate the anchor.
- Player effect/muzzle anchor: source pixel **(25,15)** — coincides with the revolver muzzle when the arm is extended forward at hip height.
- Player is rendered at **integer 2x scale** (64x64 on screen). The larger canvas gives Neo-Geo-fighting-game-tier detail (wide-brim hat silhouette, brim-shadowed eyes, mustache, kerchief, poncho, belt buckle, boots with spurs) while the 24x48 world collision body is decoupled from sprite size in the standard platformer fashion.
- Bandit and wildlife frames share a stable bottom-aligned 16x16 source box.
- Render at integer scale with nearest-neighbor / `PointClamp`; do not filter, antialias, or resample.
- Props use bottom-center placement unless room metadata specifies another origin.

## Palette

| Role | Hex |
|---|---|
| Near-black plum outline | `#231820` |
| Deep boot/brown | `#392326` |
| Rust | `#8E422A` |
| Warm ochre | `#CB8536` |
| Lit sand | `#EFBE5F` |
| Timber | `#683C2A` |
| Timber highlight | `#A56334` |
| Bone | `#E0CC9D` |
| Distance violet | `#504865` |
| Distance blue-grey | `#536778` |
| Deep distance blue | `#364153` |
| Sage | `#5C6F53` |
| Damage red | `#BE3D30` |
| Telegraph gold | `#F1B336` |

Lighting is upper-left. Foreground silhouettes use the dark plum outline and warm ochre/rust/timber fills; distance layers use the quieter blue-grey/violet family.

## Readability rules

- Collision surfaces have an uninterrupted bright top edge and a visually solid, dark-supported underside.
- Decorative props and background silhouettes deliberately avoid long bright horizontal rims. Crates, signs, mine timber, and wagon remains use broken crowns, muted contrast, irregular silhouettes, and ground-integrated debris shapes so they cannot be mistaken for reachable platforms or active interactables.
- Backgrounds are lower saturation and contrast than actors, pickups, hazards, and terrain.
- Bandits are upright with hat and firearm; wildlife are low, wide, and forward-heavy.
- The Player silhouette reads as a wide-brim-hatted gunslinger: dark cowboy-hat brim wider than the shoulders, brim-shadowed eye slit with a single warm glint on the visible eye, thick mustache bar, red kerchief, rust poncho with a bone-colored woven stripe and shadowed hem, belt+buckle, blue pants, deep boots with a gold spur accent. Upper-left lighting is enforced by asymmetric brim and poncho highlights so the character cannot be misread as a baseball-capped generic figure.
- Currency is a diamond token, health is a heart, and ammo is a twin-cartridge box: pickup identity never depends on tint alone.
- Notice, attack, hurt, dash, defeat, and collection use silhouette, pose, particles, or motion streaks as well as color.
- Telegraph gold and damage red are accents only; their shapes remain legible in greyscale.
