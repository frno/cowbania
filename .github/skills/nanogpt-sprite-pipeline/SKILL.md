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
  (image-to-image), `--resolution`, `--quality`, `--aspect-ratio`, `--seed`. Default model is
  `nano-banana-2` (winner of the character-identity consistency bake-off — see lesson 8 below).
- `tools/nanogpt/generate_player_frames.py` — driver that regenerates the 8 base pose PNGs for
  the player sprite from a single locked style/character prompt block plus per-pose deltas.
  Chains `--reference` off the freshly-generated `hero_idle_side.png` so all subsequent poses
  stay stylistically consistent with the idle reference. This is the source-of-truth for the
  player art prompts (versioned in the repo so re-generations are reproducible).
- `tools/nanogpt/pixelate_sprite.py` — downscale/align/quantize pipeline that turns a clean
  AI-generated image into a game-ready frame: autocrop → premultiplied BOX downscale to an
  intermediate size → alpha snap → palette snap → majority (mode) downscale to final 32×32 →
  mode-filter cleanup → erase tiny disconnected islands → cap distinct colors → place so feet
  land on the exact anchor pixel → validate. See lesson 10 below for why each stage matters.
- `tools/nanogpt/model_bakeoff.py` — one-shot comparison harness: runs one locked prompt across
  ~two dozen NanoGPT image models and builds a nearest-neighbor-upscaled contact sheet so a
  human/coordinator can pick the winning model. Rerun this if the model landscape changes
  significantly or when adding a new actor family.

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

   Same failure mode for **shooting / arm-extended poses**: an AI-generated cowboy in a wide
   textbook shooting stance (arm fully extended forward + coat tail flared behind + feet splayed)
   easily reaches a 1:1 or wider aspect ratio at source resolution. Fit-by-height then clips the
   gun barrel and the coat tail off the 32×32 canvas — the game frame ends up showing the body
   with no visible gun at all, reading as a "blob". Prompt explicitly for a **compact silhouette**:
   *"feet close together, elbow bent, forearm horizontal, revolver held at chest height and only
   sticking out a short distance in front of the body (NOT full arm-length away), coat tail hangs
   straight down (NOT flared behind), character silhouette fits into a tall narrow frame"*. Also
   describe the weapon silhouette in unambiguous terms — *"obvious barrel pointing right, trigger
   guard visible, one clear hand gripping it, reads as 'gun' at a glance"* — so the barrel doesn't
   dissolve into the fist at 32×32.
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
8. **Model choice matters more than prompt heroics — pick a model that natively produces flat
   chunky pixel-art sprite output.** AI image models default to smooth, gradient-heavy, subtly
   antialiased output even when asked for "pixel art" — visually cute at source resolution but
   disastrous downstream because gradients survive the downscale as per-pixel dither/speckle
   instead of collapsing into clean flat regions. Some models also produce **thin/spindly**
   silhouettes that lose their character features entirely after fit-by-height. Explicit
   prompting helps but it can't overcome a fundamentally photorealism-biased base model.

   Run a **model bake-off** (`tools/nanogpt/model_bakeoff.py`) with one locked prompt across
   ~20 diverse models before committing to a default. Evaluate BOTH the raw output AND the
   final 32×32 pipeline output — a raw that looks great at source resolution can still go thin
   or muddy after downscale. Rank by: (a) genuinely flat solid color regions at source, (b) hard
   1-pixel outlines, (c) chunky sturdy proportions with proportionally large head (NOT thin,
   NOT spindly, NOT anime-tall), (d) 32×32 survival: hat / bandana / coat / boots still readable
   as distinct color blocks.

   Current (2026-09, updated after full-motion test rejected seedream) shortlist from the
   Cowbania bake-offs (24 models in the style bake-off, then 8 in a follow-up
   character-identity-consistency bake-off using the existing idle as reference):

   - **Recommended default: `nano-banana-2`.** Strong image-to-image reference adherence — the
     same face, skin tone, palette, hat, coat, and proportions are preserved across all 8
     independent pose generations off one idle reference. This is the requirement that killed
     `seedream-v5.0-lite` in production: seedream produces beautiful chunky one-shot pixel art
     but drifts on identity across independent generations even with `--reference`, so the 8
     poses looked like 8 different characters (face went white → brown → black, proportions
     shifted). Character-sheet work needs identity > per-frame style. Set as `MODEL` in
     `generate_player_frames.py`. Note: nano-banana bakes transparency into a checkerboard
     preview (grey squares in RGB, not a real alpha channel) — the pipeline's flood-fill
     background remover handles this generically by seeding on corner colors rather than
     using a fixed brightness threshold.
   - **Fallback: `nano-banana-pro`.** Same family, slightly softer detail, similar identity
     hold. Use if nano-banana-2 unexpectedly muffs a specific pose.
   - **Fallback: `seedream-v5.0-lite`.** Still the cleanest per-frame chunky style if you only
     need ONE pose (concept art, tile, prop) and don't care about cross-frame identity.
   - **Fallback: `krea/v2/large/text-to-image`.** Clean flat pixel art, no reference support.

   Explicitly ruled out from consideration (do NOT use for character sheets): all Flux 2
   variants (painterly, go thin after downscale), Ideogram v4, Microsoft mai-image-2.6, OpenAI
   gpt-image-2.5/flare (previous default — dropped for cross-pose identity drift), Seedream
   v4, HiDream, Pixelwave (misleading name, is actually SDXL photorealistic), DreamShaper XL.
   `seedream-v4.5-sequential` sounded promising for a "consistent sequence" but on the
   consistency bake-off it produced a completely different child-like character in the run
   pose from the same reference — nano-banana beat it decisively.

9. **Prompt for FLAT retro pixel art AND for CHUNKY proportions.** Even with a well-chosen
   model, the prompt has to explicitly steer toward the target aesthetic. Concrete constraints
   that reliably work:
   - *"Chunky retro 16-bit game sprite (SNES / Sega Genesis era)."*
   - *"Flat solid color fills only. Absolutely NO gradients. NO soft shading. NO ambient
     occlusion. NO anti-aliasing. NO dithering."*
   - *"Hard clean 1-pixel-wide dark outlines around every shape."*
   - *"6 to 8 solid color regions total."*
   - *"Chunky sturdy thick-limbed proportions with a proportionally LARGE head — a stocky
     readable game-sprite silhouette. NOT thin, NOT spindly, NOT a tall skinny anime figure."*
   - *"Character silhouette fits a tall narrow rectangle (roughly 3:5 aspect, taller than wide)."*

   Compare a candidate reference image to an already-shipped hand-authored asset from the same
   project (e.g. `Assets/Art/Frontier/Bandit/patrol_0.png`) — count distinct colors and
   connected flat regions; if the AI candidate has >2x either metric at equivalent resolution,
   regenerate with a stronger flatness prompt before locking it as the style reference.

   Since there are no artists on this project, **the AI generation + a light downscale pipeline
   IS the production path**. Don't lean on the post-processing pipeline to salvage weak sources.
   Rewrite the prompt (more concrete, more constraints) and/or switch models; the marginal cost
   of a couple more API calls is trivial vs. building elaborate cleanup heuristics that will
   still look mediocre.

10. **Prompt for TRANSPARENT background, prompt AGAINST incidental scene elements, and be
    explicit about weapon/gear orientation.** Even with an explicit "transparent background"
    instruction, models frequently sprinkle atmospheric detail around action poses (dust motes,
    smoke wisps, sand grains, motion streaks) that are colored (not near-white) and therefore
    survive the chroma-key as opaque tan/rust flecks scattered across the game canvas. In the
    final 32×32 they read as random speckle noise. Add explicit negative language to every
    prompt: **"NO backdrop, NO ground plane, NO shadow, NO dust particles, NO smoke, NO
    speckles, NO scenery, NO text, NO labels — NOTHING except the character silhouette"**.

    Same discipline applies to gear/weapon orientation. Models default to "cinematic" gun
    renders — a **holstered** sidearm renders as pointing FORWARD from the hip (looks
    nonsensical: like the character is about to fire from a holstered gun) unless you spell out
    the correct real-world orientation: *"the revolver is HOLSTERED on the right hip, grip
    points STRAIGHT UP out of the holster, barrel points STRAIGHT DOWN into the holster, NOT
    pointing forward, gun is NOT drawn"*. And separately for the shooting frames: *"holster is
    EMPTY — revolver is drawn and held in the hand"* so the model doesn't try to render the
    holster full AND a duplicate gun in the hand.

    The pipeline erases tiny disconnected islands as a safety net, but the safety net is not a
    substitute for a clean, correctly-composed source.

    On backgrounds specifically: don't rely on a single brightness threshold to chroma-key.
    Different models bake "transparency" into RGB differently — solid near-white, light-grey
    checkerboard, dark-grey checkerboard, tinted checkerboard. `pixelate_sprite._load_rgba`
    uses a corner-seeded flood-fill: sample the four corners' colors, allow any low-saturation
    pixel within a luminance window of a seed to become transparent, walk outward until we hit
    a saturated or off-window pixel (the character's outline). This crosses checker squares of
    any brightness without accidentally erasing dark character features. Any new model that
    ships a weirder BG (tinted, textured, gradient) will need this rule reviewed.
11. **The downscale/quantize pipeline is the second-biggest lever on cleanliness.** Naïvely
    quantizing each pixel to the nearest palette color AFTER downscaling a smooth AI image
    just recolors the gradient noise into palette-noise; it does not remove the underlying
    softness. What actually produces deliberate-looking pixel art:
    - **Pre-quantize BEFORE the final downscale.** Palette-snap at an intermediate resolution
      (e.g. 4× the target) so flat regions exist upstream, not per-pixel dither downstream.
    - **Use majority (mode) downscaling for the final step**, not averaging/LANCZOS. Each output
      pixel becomes the most common color of its source patch, which can only produce a color
      that was already flat there — averaging invents mid-tones that then re-quantize into
      speckle.
    - **Run a 3×3 mode-filter cleanup pass (twice) on the final canvas** to eliminate salt-and-
      pepper speckle (isolated single-color pixels replaced by their neighborhood majority).
    - **Erase small disconnected islands.** Any opaque 8-connected component smaller than ~4
      pixels is almost always dust/particles from the AI source (smoke, sand, muzzle debris) —
      the chroma-key can't catch them because they aren't near-white. Kill them post-quantize.
    - **Cap distinct colors per frame.** Cowbania Frontier hand-art uses ~5-7 colors per pose
      on 16×16; target ~6 on 32×32. Remap rare (bottom-ranked) opaque colors to the nearest of
      the top-N dominants — never to a brand-new hue — so the palette contract with existing
      art holds.
    The `tools/nanogpt/pixelate_sprite.py` `_fit_into_canvas` step implements all five of these.

12. **Foreground must READ against the actual game background.** A sprite reviewed in
    isolation (transparent viewer, black bg, checkerboard) is not the same test as the sprite
    composited on the game's actual dusk/purple parallax bands. Do the composite test
    explicitly: paste each final frame on top of `Frontier/Background/hub_mid.png`,
    `branch_far.png`, a solid deep-blue `(54,65,83)`, a solid dusk-purple `(80,72,101)`,
    and pure black — build a contact sheet, look at whether the silhouette reads.

    Two failure modes to watch for:
    - **Palette collision with background.** If the character uses a palette color that is
      *also* a dominant background color, that region will literally disappear. This bit us
      with the player's jeans (`deep-distance-blue 0x36,0x41,0x53`) — identical to the sky
      band. Fix: remap the collision color to a lighter neighbor (`blue-grey 0x53,0x67,0x78`
      for jeans). Done as a global find-and-replace pass in `_boost_readability`.
    - **Near-black outline blending into a dark bg band.** The character's default outline
      color `0x23,0x18,0x20` is nearly identical to the game's darkest bg values, so the
      silhouette dissolves into any low-luminance region. Fix: lift ONLY the outer-edge
      outline pixels (those touching a transparent 8-neighbor) to a warm mid-brown
      (`timber 0x68,0x3C,0x2A`) so the character has a legible "warm rim" against cool dark
      bg. Interior outline pixels stay dark so shading contrast is preserved. Same pass.

    Directive: "colors need to reflect what's foreground and what the player can interact with."
    Any bg palette change requires re-review of `_boost_readability`'s mapping constants.

13. **Smaller-canvas actors (16×16) need a different pipeline profile.** The 32×32 player
    pipeline does not port straight to 16×16 non-player actors (Bandit, Wildlife). Lessons
    from `tools/nanogpt/pixelate_enemy.py`:

    - **`_fit_into_canvas` from `pixelate_sprite` is height-first — it will overflow width
      for horizontal subjects (the wildlife quadruped is ~1.7× wider than tall). Use a
      *contain* fit instead: `scale = min(canvas_w / w, canvas_h / h)`.** Both tall (bandit)
      and wide (wildlife) subjects then land inside the 16-pixel bounds.
    - **Drop `TARGET_COLORS_PER_FRAME` from 6 → 4.** 16×16 = 256 pixels total; more than
      ~4 dominant colors reintroduces the per-pixel speckle look. Hand-authored Frontier
      enemies also sat at ~4-5 colors.
    - **The `_leg_shift` walk-cycle derivation doesn't apply below ~24 pixels tall.** The
      "legs" downsample to 1-column-wide features and split-shifting disconnects the feet
      from the torso. Use a whole-sprite bob-cycle instead (`_shift(base, 0, dy)` with
      `dy ∈ {0, -1}`) — still reads as a walk animation without introducing stray
      components.
    - **Add a component-bridging post-process.** Even without leg-shift, an AI source with
      thin legs will occasionally downsample into torso-component + feet-component with a
      1-2 row gap that the feet-anchor guarantee can't span (its 1-pixel column bridge
      hits an already-opaque feet cell and stops). Implement `_ensure_single_component`:
      after the pipeline runs, if 8-connected components > 1, draw a 1-pixel Manhattan
      bridge from the smaller component to the nearest main-component cell in the sprite's
      own dominant color. Run this only on the base processed source; intentional stamps
      (notice glyphs, muzzle flashes, dust puffs, lunge streaks) are added AFTER and are
      allowed to remain disconnected — that's how they read as accents rather than shading.
    - **Feet-anchor bridge budget shrinks to ~4 rows on 16×16** (from 5 on 32×32) — a
      5-pixel bridge on a 16-pixel canvas is nearly half the sprite.
    - **Notice indicator glyphs may not fit above the head at 16×16.** For a full-height
      subject (bandit fills all 16 rows), the `!` glyph anchored at `(8, 2)` lands *inside*
      the head silhouette. It still reads as a brief gold flash there — acceptable — but
      subjects with room above the head (wildlife's low quadruped) get a much more legible
      standalone indicator.
    - **Shared style/palette prompt language ports directly.** The cowboy `STYLE` and
      `BACKGROUND` blocks (flat colors, no gradients, no AA, hard outlines, transparent bg,
      no scenery/text) work verbatim for enemy prompts. What must change per-actor is the
      `CHARACTER` block (silhouette / body plan / palette accents to keep enemies distinct
      from the player and from each other).

14. **Parallax background layers are their own pipeline profile.** See
    `tools/nanogpt/pixelate_background.py`. Backgrounds are 256×144 RGBA silhouette bands
    that tile horizontally in `StageRenderer.DrawBackgroundBand`. Lessons:

    - **Do not try to hit a shared palette from the AI directly for backgrounds.** Ask for
      "two solid colors, sky + silhouette" in the prompt, but classify the actual source
      pixels by **RGB distance to a SAMPLED sky color** (majority color of the top strip
      of the source), not to the target Frontier hex. AIs render silhouette-band bgs with
      slightly-off sky hues per generation, so a fixed target-hex classifier over- or
      under-shoots depending on the run. Threshold radius `~30 RGB units` is a good
      middle ground: keeps sky-tone variance, excludes even darker "distant feature"
      pixels (see next).
    - **AI silhouette-band prompts often produce THREE tones, not two.** Distant buildings,
      pine tops, or telegraph poles frequently come out as a THIRD color that's darker
      than both sky and the intended silhouette color (the AI's own instinct for "far
      = darker"). A distance-to-sil classifier would drop them into the sky bucket; a
      distance-from-sky threshold correctly lumps them all into the silhouette bucket.
    - **Classify at source resolution, downscale with "any-silhouette-wins" per patch.**
      Naïve LANCZOS-then-classify or classify-then-mode-downscale both erase narrow
      features (telegraph poles are ~2 source pixels wide → majority-vote loses to
      surrounding sky). Instead: classify every source pixel, then for each 256×144
      output cell, set silhouette if ANY source pixel in its patch was classified as
      silhouette. Backgrounds don't need pristine flat edges — a slightly rougher
      silhouette top reads fine on a receding parallax layer.
    - **Seam-repair for horizontal wrap.** The game wraps by texture width, so the
      right edge must visually meet the left edge. Compute a per-column height map of
      the silhouette (topmost opaque row per column), then linearly interpolate
      symmetric column pairs toward their midpoint using weight `(1 - i/N)` where `i`
      is distance-from-seam and `N` is the blend zone (~32 columns). Rebuild the
      silhouette from the blended heights. This is cheap because silhouettes are
      ground-anchored: only the top edge needs blending. Validation must assert
      `heights[0] == heights[-1]` after the pass.
    - **Backgrounds must RECEDE — they are the flip side of lesson 12's readability
      rule.** Enforce very limited palette (2 colors per layer, sky + silhouette),
      low saturation, no interior detail. Palette assignments used here:
        - far layers: sky = `distance_violet 0x504865`, silhouette = `blue_grey 0x536778`
        - mid layers: sky = `dusk_plum 0x392326`, silhouette = `dusk_plum_hi 0x644c5b`
      Composite-test every new bg with actual finalized actor sprites on top BEFORE
      accepting — the actor palette was tuned against the previous bgs and any bg
      palette shift risks new collisions.
    - **Prompt must explicitly reject sky detail.** No sun, no moon, no stars, no
      clouds, no birds, no atmospheric haze — anything the AI adds to the sky ruins
      the pure-transparent-sky contract and confuses the RGB-distance classifier.

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
