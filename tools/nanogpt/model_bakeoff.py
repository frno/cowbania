"""One-shot model bake-off for Cowbania cowboy sprite generation.

Uses ONE locked prompt (see PROMPT below) and generates a single cowboy
idle-pose reference image from each of a diverse spread of NanoGPT image
models. Each raw output is then run through the existing 32x32
`pixelate_sprite.process_source` pipeline unchanged, so we can compare
which model's native style survives our downscale to actual game
resolution without going thin / speckled / muddy.

Not committed / not part of shipping pipeline — this file only exists to
support picking a winner model, after which the winner will be used to
regenerate the full 26-frame player set with the normal flow.

Usage:
    python tools/nanogpt/model_bakeoff.py
    python tools/nanogpt/model_bakeoff.py --only flux-2-pro,recraft-v3
    python tools/nanogpt/model_bakeoff.py --skip-generate     # just rebuild the contact sheet from existing PNGs
"""

from __future__ import annotations

import argparse
import re
import sys
import traceback
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
OUT = TOOLS / "out"
OUT.mkdir(parents=True, exist_ok=True)

sys.path.insert(0, str(TOOLS))
import generate_image  # noqa: E402
import pixelate_sprite  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402

PROMPT = """A single cowboy character, right-facing side profile, isolated single subject centered in frame, standing in a confident tough idle pose with feet close together.

CHARACTER (must be legible at a glance):
- Wide-brim brown cowboy hat with a darker hatband.
- Red bandana / neckerchief around the neck.
- Dark brown / near-black long duster coat that hangs straight down (does NOT flare or spread wide).
- Brown leather gunbelt with visible holster on the hip carrying a revolver.
- Dark blue jeans and brown boots visible below the coat.
- Chunky, sturdy, thick-limbed proportions — a stocky readable game-sprite silhouette. NOT a thin spindly figure. NOT a tall skinny anime figure. Head is proportionally large.
- Confident tough attitude — chin up, shoulders squared.

STYLE (critical):
- Chunky retro 16-bit game sprite (SNES / Sega Genesis era).
- Flat solid color fills only. Absolutely NO gradients. NO soft shading. NO ambient occlusion. NO anti-aliasing. NO dithering.
- Hard clean 1-pixel-wide dark outlines around every shape.
- 6 to 8 solid color regions total. Deliberate blocky pixel art, not smooth digital painting.
- The character silhouette fits a tall narrow rectangle (roughly 3:5 aspect, taller than wide).

BACKGROUND (critical):
- Fully transparent background. Pure alpha zero.
- NO backdrop, NO ground plane, NO shadow, NO dust particles, NO smoke, NO speckles, NO scenery, NO text, NO labels. NOTHING except the character silhouette."""

# Diverse spread across families/providers. Avoids nsfw-flagged models.
# Some may fail per-call (API errors, unsupported params) — those are
# captured and reported but do not stop the run.
MODELS: list[str] = [
    "openai/gpt-image-2.5/flare/text-to-image",   # current default
    "openai/gpt-image-2.5/sunburst/text-to-image",
    "flux-2-pro",
    "flux-2-dev",
    "flux-2-flash",
    "flux-schnell",
    "qwen-image-max",
    "seedream-v4",
    "seedream-v5.0-lite",
    "ideogram/v4/instant",
    "ideogram/v4/fast",
    "recraft-v3",
    "recraft-v4",
    "nano-banana-2",
    "nano-banana-pro",
    "dall-e-3",
    "hidream",
    "lucid-origin",
    "imagen-3.0-generate-002",
    "grok-2-image",
    "krea/v2/large/text-to-image",
    "microsoft/mai-image-2.6",
    "pixelwave",
    "dreamshaper-xl",
]


def _slug(model: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", model.lower()).strip("-")


def _raw_path(model: str) -> Path:
    return OUT / f"compare_{_slug(model)}_raw.png"


def _small_path(model: str) -> Path:
    return OUT / f"compare_{_slug(model)}_32x32.png"


def _generate_one(model: str) -> tuple[bool, str]:
    dest = _raw_path(model)
    # Progressive fallback on parameters: some models reject resolution/quality/aspect.
    attempts = [
        {"resolution": "1k", "quality": "high", "aspect_ratio": "1:1"},
        {"resolution": "1024x1024"},
        {"aspect_ratio": "1:1"},
        {},
    ]
    last_err = ""
    for params in attempts:
        try:
            generate_image.generate(
                PROMPT,
                model,
                dest,
                resolution=params.get("resolution"),
                quality=params.get("quality"),
                aspect_ratio=params.get("aspect_ratio"),
                n=1,
                seed=None,
                reference=None,
            )
            return True, str(dest)
        except SystemExit as exc:
            last_err = f"SystemExit({exc.code})"
        except Exception as exc:
            last_err = f"{type(exc).__name__}: {exc}"
    return False, last_err


def _downscale_one(model: str) -> tuple[bool, str]:
    raw = _raw_path(model)
    if not raw.exists():
        return False, "missing raw"
    try:
        im = pixelate_sprite.process_source(raw)
        im = pixelate_sprite._clip_below_feet(im)
        im = pixelate_sprite._guarantee_feet_anchor(im)
        im.save(_small_path(model))
        return True, "ok"
    except Exception as exc:
        return False, f"{type(exc).__name__}: {exc}"


def _build_contact_sheet(models: list[str]) -> Path:
    # Layout: grid of cells. Each cell = raw thumbnail (200x200) on top,
    # 32x32 upscaled to 320x320 nearest-neighbor below, model label
    # underneath. 3 columns.
    cols = 3
    cell_w = 340
    cell_h = 200 + 8 + 320 + 40  # raw + gap + downscaled + text
    pad = 12
    rows = (len(models) + cols - 1) // cols
    sheet_w = cols * cell_w + (cols + 1) * pad
    sheet_h = rows * cell_h + (rows + 1) * pad + 40
    sheet = Image.new("RGBA", (sheet_w, sheet_h), (40, 40, 48, 255))
    draw = ImageDraw.Draw(sheet)
    try:
        title_font = ImageFont.truetype("arial.ttf", 22)
        label_font = ImageFont.truetype("arial.ttf", 16)
    except Exception:
        title_font = ImageFont.load_default()
        label_font = ImageFont.load_default()
    draw.text((pad, 8), f"Cowboy sprite model bake-off ({len(models)} models, same locked prompt)", fill=(240, 240, 240, 255), font=title_font)

    for i, model in enumerate(models):
        r, c = divmod(i, cols)
        x0 = pad + c * (cell_w + pad)
        y0 = 40 + pad + r * (cell_h + pad)
        draw.rectangle((x0, y0, x0 + cell_w, y0 + cell_h), fill=(60, 60, 72, 255))

        raw_path = _raw_path(model)
        small_path = _small_path(model)

        # Raw thumbnail area
        raw_box = (x0 + 10, y0 + 10, x0 + 10 + 200, y0 + 10 + 200)
        if raw_path.exists():
            try:
                raw = Image.open(raw_path).convert("RGBA")
                raw.thumbnail((200, 200), Image.LANCZOS)
                bg = Image.new("RGBA", (200, 200), (90, 90, 100, 255))
                px = (200 - raw.width) // 2
                py = (200 - raw.height) // 2
                bg.paste(raw, (px, py), raw)
                sheet.paste(bg, (raw_box[0], raw_box[1]))
            except Exception as exc:
                draw.text((raw_box[0] + 4, raw_box[1] + 4), f"raw load err:\n{exc}", fill=(255, 120, 120, 255), font=label_font)
        else:
            draw.text((raw_box[0] + 4, raw_box[1] + 4), "raw missing\n(gen failed)", fill=(255, 120, 120, 255), font=label_font)

        # Downscaled 32x32 at 10x nearest-neighbor
        small_box_x = x0 + 10
        small_box_y = raw_box[3] + 8
        if small_path.exists():
            try:
                small = Image.open(small_path).convert("RGBA")
                zoomed = small.resize((320, 320), Image.NEAREST)
                bg = Image.new("RGBA", (320, 320), (255, 255, 255, 255))
                bg.alpha_composite(zoomed)
                sheet.paste(bg, (small_box_x, small_box_y))
            except Exception as exc:
                draw.text((small_box_x + 4, small_box_y + 4), f"32x32 load err:\n{exc}", fill=(255, 120, 120, 255), font=label_font)
        else:
            draw.text((small_box_x + 4, small_box_y + 4), "32x32 missing", fill=(255, 120, 120, 255), font=label_font)

        # Label at bottom of cell
        draw.text((x0 + 10, y0 + cell_h - 34), model, fill=(230, 230, 230, 255), font=label_font)

    dest = OUT / "_bakeoff_contact_sheet.png"
    sheet.save(dest)
    return dest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--only", default=None, help="Comma-separated subset of models to run (must be substrings of full model IDs).")
    parser.add_argument("--skip-generate", action="store_true", help="Don't call the API, just rebuild the contact sheet from cached raw PNGs.")
    args = parser.parse_args()

    models = MODELS
    if args.only:
        wanted = [s.strip() for s in args.only.split(",") if s.strip()]
        models = [m for m in MODELS if any(w in m for w in wanted)]
        if not models:
            print("No models matched --only filter.", file=sys.stderr)
            sys.exit(1)

    print(f"Bake-off across {len(models)} models. Prompt length: {len(PROMPT)} chars.\n")

    results: list[tuple[str, str, str]] = []
    for model in models:
        if args.skip_generate and _raw_path(model).exists():
            print(f"[cached] {model}")
            gen_ok, gen_info = True, "cached"
        else:
            print(f"[generate] {model} ... ", end="", flush=True)
            gen_ok, gen_info = _generate_one(model)
            print("OK" if gen_ok else f"FAIL ({gen_info})")

        if gen_ok:
            ok, info = _downscale_one(model)
            print(f"[downscale] {model} ... {'OK' if ok else 'FAIL ('+info+')'}")
            results.append((model, "ok" if ok else f"downscale-fail: {info}", ""))
        else:
            results.append((model, f"gen-fail: {gen_info}", ""))

    print("\nBuilding contact sheet ...")
    dest = _build_contact_sheet(models)
    print(f"Wrote: {dest}")

    print("\nSummary:")
    for m, s, _ in results:
        print(f"  {m:52s} {s}")


if __name__ == "__main__":
    main()
