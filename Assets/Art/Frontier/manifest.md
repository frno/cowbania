# Release 6 Frontier Visual Identity

Locally generated pixel art for Cowbania's Dust-Gothic Frontier presentation.

- **Non-player art (Bandit, Wildlife, Pickups, Terrain, Props, Effects, UI, Backgrounds)** is generated procedurally. Run `python tools\generate_frontier_assets.py` from this directory (or invoke it by absolute path) to reproduce and validate every non-player PNG.
- **Player art** is produced by an AI-assisted pipeline (see [Player art pipeline](#player-art-pipeline) below). To regenerate the 26 player PNGs, run `python tools\nanogpt\pixelate_sprite.py` from the repository root.

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

## Player art pipeline

The 26 player frames are not procedurally drawn. They are produced by a two-stage pipeline that combines AI-generated pose references with a deterministic downscale/quantize step, then dropped into `Assets/Art/Frontier/Player/{state}_{frame}.png` at the exact stable filenames listed in the [asset contract](#stable-asset-contract) table.

**Stage 1 — AI pose generation** (`tools/nanogpt/generate_image.py`).

- One "hero" idle reference is generated first as a right-facing side-profile character at ~1024x1024 (`tools/nanogpt/out/hero_idle_side.png`). This is the locked style + character anchor.
- Seven per-state base poses (`hero_run.png`, `hero_jump.png`, `hero_fall.png`, `hero_shoot.png`, `hero_reload.png`, `hero_hurt.png`, `hero_dash.png`) are generated using the hero as `--reference` so hat, coat, bandana, gunbelt, boots, and proportions stay consistent across the sheet.
- Every base pose is authored as a **right-facing side profile** (the renderer mirrors via `SpriteEffects.FlipHorizontally` for left-facing motion — see `RenderContext.Anchored`). Do not commit front-facing or 3/4 poses.

**Stage 2 — Pixelate** (`tools/nanogpt/pixelate_sprite.py`).

For every source pose the pipeline:

1. Chroma-keys the AI's near-white background to transparent (the NanoGPT image API returns opaque-white backgrounds, not alpha=0).
2. Trims to the character's bounding box.
3. Downscales by height to `feet_anchor_y + 1` = 28 rows (premultiplied-alpha LANCZOS, so partially-transparent edges don't fringe blue/purple). Wide poses (run, dash, fall) overflow the 32-pixel canvas width horizontally and clip; that is the intended trade — keeping full readable body height is more valuable than including every strand of trailing coat.
4. Snaps alpha to strictly `{0, 255}` and quantizes every opaque pixel to the 14-color Frontier palette (nearest-neighbor in RGB). This removes anti-aliased mid-tones and gives the flat pixel-art look that matches the Bandit/Wildlife/prop art already in-repo.
5. Places the sprite on a 32x32 canvas so the midpoint of the bottom-band opaque pixels lands at feet anchor `(16, 27)`.
6. Guarantees the feet anchor pixel is opaque; erases any opaque pixels below row 27 (would otherwise clip through the floor).

**Frame derivation.** Only 8 AI calls are spent — one hero idle + one per non-idle state. The four idle frames come from the hero via 1-pixel breathing-bob shifts; the six run frames come from the run base via bob + leg-shift keyframes; the three shoot frames come from the shoot base with an added muzzle-flash stamp at effect anchor `(25, 15)` and a recoil offset on frame 2; reload frames cycle upper-body bobs; dash frames add horizontal speed streaks; hurt/jump/fall frames apply small pose offsets. To upgrade any frame past this derivation quality, drop a per-frame source at `tools/nanogpt/out/hero_{state}_{frame}.png` — the pipeline will prefer it over the derived variant.

**Anchors are unchanged** from the procedural asset contract: source canvas 32x32, feet anchor source pixel `(16, 27)`, effect/muzzle anchor source pixel `(25, 15)`, rendered at integer 2x scale (64x64 on-screen), `SamplerState.PointClamp`, no filtering.

`Assets/Art/Frontier/tools/generate_frontier_assets.py` still owns non-player art. Its `player()` function is retained as reference only and is no longer called by `generate()`; do not reintroduce it as the source of truth.
