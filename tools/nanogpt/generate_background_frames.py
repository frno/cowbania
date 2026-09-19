"""Generate the 4 Frontier background source images for Cowbania.

Produces one AI landscape per parallax layer into `tools/nanogpt/out/`,
which `pixelate_background.py` then quantizes / silhouettes / seam-repairs
into game-ready 256x144 tileable PNGs.

Two stages, two parallax bands each — exactly matching the stable asset
contract in `Assets/Art/Frontier/manifest.md`:

* `hub_far`  — Western frontier town, distant band. Cool blue-grey palette.
* `hub_mid`  — Same town, closer band with more prominent silhouettes.
              Warmer plum/violet palette (matches the existing procedural
              art's cool/warm depth cue).
* `branch_far` — Canyon / mine wilderness, distant band.
* `branch_mid` — Canyon / mine wilderness, closer band.

Model: nano-banana-2 — the same choice that won the character bake-off
and worked well for enemies. Backgrounds don't need reference-chained
identity consistency (each parallax layer is its own composition), so
each of the 4 layers is generated independently text-to-image.

Generated at 16:9 aspect ratio so the wide horizontal composition maps
cleanly onto the 256x144 canvas without letterboxing.

Prompt discipline — critical for backgrounds specifically:

* HORIZONTAL wide scene, NOT a portrait or square composition.
* Extreme silhouette style: only 2 solid color regions (sky + silhouette
  band). No sky gradients, no cloud detail, no atmospheric fade. Pushes
  the AI toward the flat two-color silhouette-band style the existing
  Frontier bgs use — anything more complicated wrecks the "backgrounds
  must recede, not compete with actors" readability rule.
* Explicitly low saturation + low contrast — background layers must
  RECEDE from foreground actors. Anything vibrant becomes a distraction.
* No text, no captions, no ground plane, no people, no vehicles.

Usage:
    python tools/nanogpt/generate_background_frames.py         # all 4
    python tools/nanogpt/generate_background_frames.py --layer hub_far
    python tools/nanogpt/generate_background_frames.py --dry-run
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
OUT = TOOLS / "out"
OUT.mkdir(parents=True, exist_ok=True)

sys.path.insert(0, str(TOOLS))
import generate_image  # noqa: E402

MODEL = "nano-banana-2"
RESOLUTION = "1k"
QUALITY = "high"
ASPECT_RATIO = "16:9"

STYLE = """STYLE (critical):
- Chunky retro 16-bit game background art (SNES / Sega Genesis era).
- EXTREME SILHOUETTE STYLE: only 2 solid color regions per image — sky, and one silhouette band.
- Flat solid color fills only. Absolutely NO gradients. NO soft shading. NO atmospheric fade. NO clouds. NO stars. NO textures. NO anti-aliasing. NO dithering.
- LOW SATURATION and LOW CONTRAST — this is a receding background layer, NOT a foreground illustration. Should feel muted and far away.
- Hard clean pixel boundaries between the sky region and the silhouette region.
- The upper ~60% of the image is a pure flat SKY color. The lower ~40% contains a jagged silhouette rising from the bottom edge into the sky."""

BACKGROUND_RULES = """SCENE RULES (critical):
- WIDE horizontal composition. Landscape orientation. The scene wraps across the full horizontal width like a scrolling parallax layer.
- NO ground plane at the bottom — the silhouette band IS the ground line. Silhouette shapes rise up FROM the bottom edge.
- NO people, NO characters, NO animals, NO vehicles, NO wagons, NO text, NO captions, NO labels, NO signs.
- NO sun, NO moon, NO stars, NO clouds, NO birds — completely empty sky.
- The silhouette band contains only STATIC LANDSCAPE OR ARCHITECTURAL FEATURES arranged left-to-right across the whole width."""

LAYERS: dict[str, dict[str, str]] = {
    # -------- hub / peaceful frontier town at dusk --------
    "hub_far": {
        "palette": (
            "PALETTE: sky is a muted dark blue-purple (like #504865 / dusk violet). "
            "Silhouette band is a slightly lighter cool blue-grey (like #536778). "
            "Only these 2 colors."
        ),
        "content": (
            "CONTENT: distant Western frontier town horizon. Small square wooden "
            "buildings — HOUSES, false-front shops, a tiny church steeple — sit "
            "in a row along the bottom silhouette band. Behind and above them, "
            "very gentle low ROLLING HILL SILHOUETTES rise into the sky. The "
            "buildings are TINY — they take up only the bottom ~15% of the image "
            "— because this is the FAR distance layer. Rolling hills fill the "
            "remaining silhouette space."
        ),
    },
    "hub_mid": {
        "palette": (
            "PALETTE: sky is a muted dark plum (like #3d2f3f). Silhouette band is "
            "a warmer dusty rust-plum (like #644c5b). Only these 2 colors."
        ),
        "content": (
            "CONTENT: middle-distance Western frontier features. TALL WOODEN "
            "TELEGRAPH POLES with horizontal crossarms stand at regular intervals "
            "along the bottom silhouette band. Behind them, a MEDIUM-HEIGHT "
            "rolling mountain silhouette rises into the sky. The telegraph poles "
            "take up the middle vertical band. This is the MID parallax layer — "
            "features are larger and more prominent than the far layer."
        ),
    },
    # -------- branch / mine wilderness canyon at dusk --------
    "branch_far": {
        "palette": (
            "PALETTE: sky is a muted dark blue-purple (like #504865 / dusk violet). "
            "Silhouette band is a slightly lighter cool blue-grey (like #536778). "
            "Only these 2 colors."
        ),
        "content": (
            "CONTENT: distant CANYON RIDGE with JAGGED SHARP MOUNTAIN PEAKS "
            "cutting across the horizon. Scattered along the ridge, small "
            "triangular PINE TREE silhouettes. At the very bottom of the image, "
            "tiny distant MINE BUILDING silhouettes (small square shapes with "
            "peaked roofs). This is the FAR distance layer — jagged mountains "
            "dominate, foreground details are tiny."
        ),
    },
    "branch_mid": {
        "palette": (
            "PALETTE: sky is a muted dark plum (like #3d2f3f). Silhouette band is "
            "a warmer dusty rust-plum (like #644c5b). Only these 2 colors."
        ),
        "content": (
            "CONTENT: middle-distance canyon features. TALL TRIANGULAR PINE / "
            "FIR TREE silhouettes stand in a row along the bottom silhouette "
            "band. Behind them, medium-height rolling mountain silhouettes rise "
            "into the sky. The pine trees take up the middle vertical band. "
            "This is the MID parallax layer — trees are prominent and clearly "
            "readable as triangular fir shapes."
        ),
    },
}


def _build_prompt(layer: str) -> str:
    cfg = LAYERS[layer]
    return (
        "Wide horizontal side-scrolling GAME BACKGROUND art, empty landscape silhouette.\n\n"
        f"{cfg['content']}\n\n"
        f"{cfg['palette']}\n\n"
        f"{STYLE}\n\n"
        f"{BACKGROUND_RULES}"
    )


def _output_path(layer: str) -> Path:
    return OUT / f"bg_{layer}.png"


def _generate(layer: str) -> Path:
    dest = _output_path(layer)
    prompt = _build_prompt(layer)
    print(f"[generate] {layer} -> {dest.name}")
    generate_image.generate(
        prompt,
        MODEL,
        dest,
        resolution=RESOLUTION,
        quality=QUALITY,
        aspect_ratio=ASPECT_RATIO,
        n=1,
        seed=None,
        reference=None,
    )
    return dest


def main() -> None:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--layer", choices=tuple(LAYERS.keys()), default=None)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    layers = [args.layer] if args.layer else list(LAYERS.keys())
    if args.dry_run:
        for lyr in layers:
            print("=" * 70)
            print(f"LAYER: {lyr}")
            print("=" * 70)
            print(_build_prompt(lyr))
        return

    for lyr in layers:
        _generate(lyr)


if __name__ == "__main__":
    main()
