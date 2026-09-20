# NanoGPT Sprites

Read only for NanoGPT actor-sprite work. `Assets/Art/Frontier/manifest.md` is the authoritative asset,
frame-count, palette, and anchor contract; generation scripts contain authoritative prompts.

## Commands

```powershell
# Inspect prompts without API cost
python tools/nanogpt/generate_player_frames.py --dry-run
python tools/nanogpt/generate_enemy_frames.py --dry-run

# Generate base poses (use --state/--enemy to limit paid calls)
python tools/nanogpt/generate_player_frames.py [--state <state>]
python tools/nanogpt/generate_enemy_frames.py [--enemy <enemy>] [--state <state>]

# Build final frames under Assets/Art/Frontier
python tools/nanogpt/pixelate_sprite.py [--state <state>] [--frame <n>]
python tools/nanogpt/pixelate_enemy.py [--enemy <enemy>] [--state <state>] [--frame <n>]
```

Generators and pixelators currently share fixed sources in `tools/nanogpt/out/`:
`hero_idle_side.png`, `hero_<state>.png`, `hero_<enemy>_<state>.png`, and optional per-frame
`hero_<state>_<n>.png` / `hero_<enemy>_<state>_<n>.png`. Do not move these into a task subdirectory
unless the scripts are changed together. Check existing files before overwriting interrupted work.

## Generation rules

- Auth: `NANOGPT_API_KEY` or `tools/nanogpt/.secret/api_key.txt`. Never expose or commit it.
- Default character-sheet model: `nano-banana-2`; fallback `nano-banana-pro`. Use
  `seedream-v5.0-lite` only for isolated poses where cross-frame identity does not matter.
- Generate one isolated pose at a time. Generate the neutral reference first; chain later poses from
  it. Do not cross-chain enemy archetypes.
- Author actors as right-facing side profiles; the renderer mirrors left-facing sprites.
- Prompt for: transparent empty background; no text, scenery, shadow, dust, smoke, or stray particles;
  flat solid colors; no gradients, antialiasing, or dithering; hard outline; chunky silhouette.
- Keep action poses upright/compact enough for the canvas. For holstered guns specify grip up/barrel
  down; for shooting specify an empty holster and one clearly readable drawn gun.
- Reject a bad base pose instead of adding complex salvage code. Use derived frames only for small
  changes such as bob, recoil, or effect stamps.

## Runtime contracts

- Player: 32x32, feet `(16,27)`, effect/muzzle `(25,15)`, about 6 dominant colors.
- Enemies: 16x16, feet `(8,15)`, 4 dominant colors. Use contain-fit for wide actors, whole-sprite bob
  rather than split leg shifts, and bridge accidental disconnected body components before adding
  intentional effects.
- Preserve alpha `{0,255}`, palette quantization, stable filenames, `PointClamp`, integer scaling,
  and no opaque pixels below the feet anchor.
- Check final-resolution frames, not only source images. Review a nearest-neighbor contact sheet over
  `hub_mid.png`, `branch_far.png`, dark blue `(54,65,83)`, dusk purple `(80,72,101)`, and black.
  Silhouettes, enemies, interactables, and telegraphs must remain readable without color alone.

## Acceptance and cleanup

1. Run the pixelator; it must report no frame validation failures.
2. Review final frames/contact sheet for identity, facing, anchors, clipping, stray pixels, palette,
   animation continuity, and background contrast.
3. Update `Assets/Art/Frontier/manifest.md` when the asset contract changes.
4. Run:
   ```powershell
   dotnet build Cowbania.sln --no-restore
   dotnet run --project tests/Cowbania.Core.Tests --no-build
   dotnet run --project tests/Cowbania.Host.Tests --no-build
   ```
5. Delete this task's rejected/superseded sources and review sheets after acceptance. Retain required
   fixed-name sources while work is active and report them if interrupted. Never wipe all of
   `tools/nanogpt/out/`, `.secret/`, scripts, manifests, or approved runtime assets.