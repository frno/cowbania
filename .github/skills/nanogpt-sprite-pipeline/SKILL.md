---
name: "nanogpt-sprite-pipeline"
description: "Use when generating or regenerating Cowbania character/actor sprite art with AI image generation (NanoGPT). Do not load for procedural/non-character art (terrain, props, UI) or for non-art tasks."
domain: "art-pipeline"
confidence: "medium"
source: "earned — player sprite redesign (feature/cowboy-sprite-redesign)"
---

## Purpose

Generate game-ready, pixel-art actor sprites with an AI image model instead of (or alongside) the
procedural PIL generator, without producing unusable, off-facing, or unreadable output. This
captures what broke and what worked during the first end-to-end run (player sprite redesign).

## Tooling

- `tools/nanogpt/generate_image.py` — stdlib-only client for NanoGPT's Image API
  (`POST https://nano-gpt.com/api/v1/images`). Supports `--list-models`, `--reference <path>`
  (image-to-image), `--resolution`, `--quality`, `--aspect-ratio`, `--seed`.
- `tools/nanogpt/pixelate_sprite.py` — downscale/align/quantize pipeline that turns a clean
  AI-generated image into a game-ready frame: autocrop → fit-by-height into the canvas →
  alpha snap → palette quantization → place so feet land on the exact anchor pixel → validate.

## Auth — never handled via chat

- Key is read from the `NANOGPT_API_KEY` env var, or (fallback, since env vars set in a
  different shell/window do not propagate to this tool's fresh processes) a gitignored local
  file `tools/nanogpt/.secret/api_key.txt` containing only the key.
- **Never** ask the user to paste the key in chat, never echo it, never write it to any tracked
  file. If a key is ever pasted in chat, treat it as compromised and tell the user to rotate it
  immediately — do not reuse it.
- `tools/nanogpt/.secret/` and `tools/nanogpt/out/` (scratch generations) are both gitignored;
  only the scripts themselves and final game assets are tracked.

## Hard-earned lessons (read before generating anything)

1. **A "concept sheet" is not a sprite sheet.** A single prompt describing "a reference sheet with
   front/side/back views, expressions, a palette swatch, action poses" produces a nice mood board
   but with inconsistent frame sizes/positions and no usable grid — it cannot be sliced into game
   frames. Use it only as a locked style reference for later isolated generations, never as a
   source to crop from.
2. **Generate ONE pose at a time, isolated, transparent background, no text/labels/other panels,
   centered subject.** Use `--reference <locked-style-image>` (image-to-image) on every subsequent
   call so the character (hat, coat, palette, proportions) stays consistent across all frames
   instead of drifting style-to-style.
3. **Facing direction is a hard game-engine constraint, not a style choice.** Check the renderer's
   flip convention before generating anything (`SpriteEffects.FlipHorizontally` triggered by
   `facingDirection < 0` in `RenderContext.Anchored`, or equivalent) — Cowbania actors are authored
   as a single **right-facing side-profile** pose and mirrored for the left. A front-facing or
   wrong-facing generation is unusable no matter how good it looks; verify facing before spending
   more calls refining a pose.
4. **Motion/action poses (dash, leap, hurt) are the most likely to fail.** A base pose that reads
   as "lying flat / horizontal / parallel to the ground" will collapse into an unreadable blob once
   fit-by-height into a small square canvas. Explicitly prompt for an upright/leaning pose
   ("~30-45° forward lean, head above torso above legs, NOT lying flat or leaping parallel to the
   ground") for any pose implying fast horizontal motion.
5. **Always view the actual final game-resolution PNG before accepting it**, not just the
   high-resolution AI output. Upscale with nearest-neighbor (no smoothing) to inspect — a pose that
   looks fine at source resolution can become unreadable mush after quantization/downscale. Build a
   quick review contact-sheet (nearest-neighbor tile grid) rather than eyeballing 32x32 pixels
   directly.
6. **Cost is cheap relative to engineering time — don't be stingy.** Prefer a fresh AI generation
   per frame (with the locked reference) over derivation tricks (pixel-shift/overlay/stretch) when
   quality is in doubt. Derivation (bob/lean/leg-shift offsets, stamped overlays like a muzzle
   flash) is fine for near-duplicate frames within one pose family (e.g. idle breathing, walk
   cycle keyframes) but is not a substitute for a correct base pose — a bad base pose cannot be
   salvaged by clever pixel manipulation. When in doubt, spend another API call.
7. **Reuse the established palette/anchor contract.** Keep new actor art on the same feet-anchor
   pixel and quantized to the existing Frontier palette family (see
   `Assets/Art/Frontier/manifest.md`) so new sprites don't look visually foreign next to already
   shipped art, and so anchor math in `FrontierAnimationCatalog` / `ActorRenderer` doesn't need
   re-tuning per actor.

## Workflow

1. Generate ONE locked reference pose (the most neutral/idle pose, right-facing, isolated,
   transparent bg) and confirm by eye that it reads correctly (hat/silhouette/attitude/facing)
   before generating anything else.
2. Generate each remaining required pose using `--reference` pointing at that locked image, with a
   prompt describing the specific silhouette read for that pose (arm extended + muzzle flash for
   shoot, tucked legs for jump, forward lean for dash, flinch/knockback for hurt, etc).
3. Run each through `pixelate_sprite.py` (or a similar per-project pipeline) to autocrop, fit to
   canvas by feet anchor, quantize to palette, and validate (exact canvas size, opaque feet-anchor
   row, nothing rendered below the anchor row).
4. Build a nearest-neighbor upscaled contact sheet of the final output frames and actually look at
   it before reporting completion or before committing.
5. Update the asset manifest (dimensions/anchors/frame list/palette/pipeline notes) alongside the
   PNGs so documentation and pixels never drift apart.

## Validation

Same as any Cowbania asset change — run from repo root:

```powershell
dotnet build Cowbania.sln --no-restore
dotnet run --project tests\Cowbania.Core.Tests --no-restore
dotnet run --project tests\Cowbania.Host.Tests --no-restore
```
