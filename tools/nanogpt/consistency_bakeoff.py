"""Character-consistency bake-off across image-to-image models.

Locked variables:
  * Reference image: tools/nanogpt/out/hero_idle_side.png (existing seedream idle).
  * 3 pose prompts: run, shoot, hurt (from generate_player_frames.POSES).

Independent variable: model.

Output:
  * out/consistency/<model_slug>/<pose>.png     raw generation
  * out/consistency/<model_slug>/<pose>_32.png  after pixelate downscale
  * out/_consistency_sheet.png                  contact sheet with model rows

Also prints a per-model face-region color summary so we can quickly see
which model kept the skin tone / palette closest to the reference.
"""

from __future__ import annotations

import sys
import traceback
from pathlib import Path
from PIL import Image

TOOLS = Path(__file__).resolve().parent
OUT = TOOLS / "out"
CONSIST = OUT / "consistency"
CONSIST.mkdir(parents=True, exist_ok=True)

sys.path.insert(0, str(TOOLS))
import generate_image  # noqa: E402
import generate_player_frames as gpf  # noqa: E402
import pixelate_sprite  # noqa: E402

REFERENCE = OUT / "hero_idle_side.png"

# Models to test. All support image_to_image.
MODELS = [
    ("nano-banana-pro", "nano_banana_pro"),
    ("nano-banana-2", "nano_banana_2"),
    ("nano-banana-edit", "nano_banana_edit"),
    ("seedream-v4.5-sequential", "seedream_v45_seq"),
    ("openai/gpt-image-2.5/flare/edit", "gpt_image_flare_edit"),
    ("seedream-5-alternative/edit", "seedream_5_alt_edit"),
    ("qwen-image-max-edit", "qwen_image_max_edit"),
    ("gpt-image-1.5", "gpt_image_1_5"),
]

POSES = ["run", "shoot", "hurt"]


def _slug_dir(slug: str) -> Path:
    d = CONSIST / slug
    d.mkdir(parents=True, exist_ok=True)
    return d


def _dominant_face_colors(png_path: Path, k: int = 5) -> list[tuple[int, int, int]]:
    """Sample a rough face region (upper-center third) and return top colors."""
    im = Image.open(png_path).convert("RGB")
    w, h = im.size
    # face box roughly: x [0.35*w, 0.65*w], y [0.10*h, 0.35*h]
    box = (int(0.35 * w), int(0.10 * h), int(0.65 * w), int(0.35 * h))
    face = im.crop(box)
    face = face.quantize(colors=k, method=Image.FASTOCTREE).convert("RGB")
    colors = face.getcolors(maxcolors=k * 4) or []
    colors.sort(reverse=True)
    return [c[1] for c in colors[:k]]


def _try_generate(model_id: str, slug: str, pose_key: str) -> Path | None:
    dest = _slug_dir(slug) / f"{pose_key}.png"
    prompt = gpf._build_prompt(pose_key)
    try:
        generate_image.generate(
            prompt,
            model_id,
            dest,
            resolution="1k",
            quality="high",
            aspect_ratio="1:1",
            n=1,
            seed=None,
            reference=REFERENCE,
        )
        return dest
    except SystemExit:
        print(f"    !! {model_id} on pose {pose_key}: API/HTTP error, skipping")
        return None
    except Exception:
        traceback.print_exc()
        return None


def _downscale(raw: Path, out32: Path) -> None:
    """Run the pixelate pipeline's full load->fit path on the raw png."""
    im = pixelate_sprite._load_rgba(raw)
    autocropped = pixelate_sprite._autocrop(im)
    fitted = pixelate_sprite._fit_into_canvas(autocropped, pixelate_sprite.CANVAS)
    up = fitted.resize((fitted.width * 8, fitted.height * 8), Image.NEAREST)
    up.save(out32)


def _make_sheet(rows: list[tuple[str, dict[str, Path]]]) -> Path:
    """rows: [(slug, {pose_key: raw_or_32_path})]. Each model = 2 rows (raw, 32)."""
    cell = 256
    padding = 8
    n_models = len(rows)
    n_cols = 1 + len(POSES)  # first col = model label
    sheet_w = n_cols * (cell + padding) + padding
    sheet_h = n_models * (2 * (cell + padding) + padding) + padding * 2

    sheet = Image.new("RGBA", (sheet_w, sheet_h), (30, 30, 30, 255))
    from PIL import ImageDraw, ImageFont

    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("arial.ttf", 14)
    except OSError:
        font = ImageFont.load_default()

    y = padding
    for slug, per_pose in rows:
        # Row A: raw
        draw.text((padding, y + 8), f"{slug}\n(raw)", fill=(220, 220, 220, 255), font=font)
        for i, pose in enumerate(POSES):
            x = (i + 1) * (cell + padding) + padding
            p = per_pose.get(f"{pose}_raw")
            if p and p.exists():
                im = Image.open(p).convert("RGBA")
                im.thumbnail((cell, cell), Image.LANCZOS)
                sheet.paste(im, (x + (cell - im.width) // 2, y + (cell - im.height) // 2), im)
            draw.text((x + 4, y + 4), pose, fill=(200, 200, 100, 255), font=font)
        y += cell + padding
        # Row B: 32
        draw.text((padding, y + 8), f"{slug}\n(32x32)", fill=(180, 220, 180, 255), font=font)
        for i, pose in enumerate(POSES):
            x = (i + 1) * (cell + padding) + padding
            p = per_pose.get(f"{pose}_32")
            if p and p.exists():
                im = Image.open(p).convert("RGBA")
                im.thumbnail((cell, cell), Image.NEAREST)
                sheet.paste(im, (x + (cell - im.width) // 2, y + (cell - im.height) // 2), im)
        y += cell + padding * 2

    out = OUT / "_consistency_sheet.png"
    sheet.save(out)
    return out


def main() -> None:
    if not REFERENCE.exists():
        raise SystemExit(f"Reference not found: {REFERENCE}")

    ref_colors = _dominant_face_colors(REFERENCE)
    print(f"REFERENCE face colors: {ref_colors}")
    print()

    rows: list[tuple[str, dict[str, Path]]] = []
    for model_id, slug in MODELS:
        print(f"=== {model_id} ({slug}) ===")
        per_pose: dict[str, Path] = {}
        pose_success = 0
        for pose in POSES:
            raw = _try_generate(model_id, slug, pose)
            if raw is None:
                continue
            pose_success += 1
            per_pose[f"{pose}_raw"] = raw
            out32 = raw.with_name(f"{pose}_32.png")
            try:
                _downscale(raw, out32)
                per_pose[f"{pose}_32"] = out32
            except Exception:
                traceback.print_exc()
            try:
                fc = _dominant_face_colors(raw)
                print(f"    {pose} face colors: {fc}")
            except Exception:
                pass
        print(f"  -> {pose_success}/{len(POSES)} poses generated")
        rows.append((slug, per_pose))

    sheet = _make_sheet(rows)
    print(f"\nContact sheet: {sheet}")


if __name__ == "__main__":
    main()
