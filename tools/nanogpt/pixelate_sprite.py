"""Pixelate AI-generated cowboy sprites into the game's 32x32 Frontier format.

Reads high-resolution transparent-background PNGs produced by
`tools/nanogpt/generate_image.py`, trims each to the character's bounding
box, downscales it to a 32x32 canvas preserving aspect ratio, quantizes the
result to the Frontier palette, positions the character so its feet land at
source pixel (16, 27), and writes the final RGBA PNG into
`Assets/Art/Frontier/Player/{state}_{frame}.png`.

This is the pipeline that replaces the procedural `player()` function in
`Assets/Art/Frontier/tools/generate_frontier_assets.py`. It is the source of
truth for player art.

Frame derivation
----------------
Because each AI generation call has a real dollar cost, we only spend one
generation per *pose*: an idle base pose (also serving as the locked
reference/style anchor), plus one AI-generated base pose per non-idle
animation state. Intra-state frames are then derived from that base by
cheap pixel-level operations:

  idle   4 frames: 1-pixel breathing bob on the torso.
  run    6 frames: horizontal squash + leg-shift keyframes cycling.
  jump   2 frames: base + variant with slight vertical shift and tilt.
  fall   2 frames: base + hat-brim flutter accent.
  shoot  3 frames: pre / muzzle-flash / recoil (backward torso shift).
  reload 4 frames: hand-position offsets from base.
  hurt   2 frames: base + recoil-forward accent.
  dash   3 frames: base + horizontal streak accents cycling.

If future iteration wants more animation quality per state, drop a
per-frame source PNG into `tools/nanogpt/out/hero_{state}_{frame}.png` and
this script will prefer it over the derived variant.

Usage:
    python tools/nanogpt/pixelate_sprite.py            # all 26 frames
    python tools/nanogpt/pixelate_sprite.py --state run --frame 2
"""

from __future__ import annotations

import argparse
from pathlib import Path
from typing import Sequence

from PIL import Image, ImageChops

REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = Path(__file__).resolve().parent / "out"
TARGET_DIR = REPO_ROOT / "Assets" / "Art" / "Frontier" / "Player"

CANVAS = (32, 32)
FEET_ANCHOR = (16, 27)  # source pixel (x, y) — matches renderer expectations.
EFFECT_ANCHOR = (25, 15)  # muzzle/effect anchor for right-facing frames.

# Frontier palette (from Assets/Art/Frontier/manifest.md). Every output
# pixel snaps to the nearest color in this table so the player integrates
# with existing Bandit / Wildlife / prop art.
PALETTE: list[tuple[int, int, int]] = [
    (0x23, 0x18, 0x20),   # outline (near-black plum)
    (0x39, 0x23, 0x26),   # deep boot brown
    (0x8E, 0x42, 0x2A),   # rust
    (0xCB, 0x85, 0x36),   # warm ochre
    (0xEF, 0xBE, 0x5F),   # lit sand
    (0x68, 0x3C, 0x2A),   # timber
    (0xA5, 0x63, 0x34),   # timber highlight
    (0xE0, 0xCC, 0x9D),   # bone
    (0x50, 0x48, 0x65),   # distance violet
    (0x53, 0x67, 0x78),   # distance blue-grey
    (0x36, 0x41, 0x53),   # deep distance blue
    (0x5C, 0x6F, 0x53),   # sage
    (0xBE, 0x3D, 0x30),   # damage red
    (0xF1, 0xB3, 0x36),   # telegraph gold
]

ALPHA_CUTOFF = 128  # pixels below this alpha become fully transparent.

STATE_COUNTS = {
    "idle": 4,
    "run": 6,
    "jump": 2,
    "fall": 2,
    "shoot": 3,
    "reload": 4,
    "hurt": 2,
    "dash": 3,
}


# ---------------------------------------------------------------------------
# Core image pipeline
# ---------------------------------------------------------------------------


def _load_rgba(path: Path) -> Image.Image:
    im = Image.open(path).convert("RGBA")
    # NanoGPT's "transparent background" outputs are sometimes actually
    # opaque near-white (e.g. (253, 253, 254, 255)) rather than truly
    # alpha-zero. Chroma-key any near-white / near-grey pixel that
    # doesn't have visible saturation to true transparent so downstream
    # trim/fit/quantize steps see the character alone.
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            # Near-white/near-grey with low saturation is background.
            mx = max(r, g, b)
            mn = min(r, g, b)
            if mx >= 230 and (mx - mn) <= 12:
                px[x, y] = (0, 0, 0, 0)
    return im


def _autocrop(im: Image.Image) -> Image.Image:
    """Crop to the tight bounding box of visible (alpha > cutoff) pixels."""
    alpha = im.getchannel("A")
    # threshold alpha into a binary mask so faint fringe pixels don't
    # inflate the bounding box.
    mask = alpha.point(lambda a: 255 if a >= ALPHA_CUTOFF else 0)
    bbox = mask.getbbox()
    if bbox is None:
        return im
    return im.crop(bbox)


def _fit_into_canvas(im: Image.Image, canvas: tuple[int, int]) -> Image.Image:
    """Downscale preserving aspect so the sprite's *height* fits above the feet row.

    We always fit by height, targeting exactly (feet_y + 1) rows — one
    row past the feet anchor — so the full character sits with its top
    at canvas row 0 and its bottom at the feet anchor row. For wide
    poses (running, dashing, falling) this lets the character's
    readable head-to-feet body span always use the full vertical
    resolution; extended limbs, coat tails, and streamers may exceed
    the 32-pixel canvas width and will be clipped when we place the
    sprite. That's a much better trade than uniformly downscaling a
    4:1-aspect pose to 8px tall.

    Uses PIL's LANCZOS on premultiplied RGBA (RGB scaled by alpha/255
    before resize, then divided back out). This avoids the classic
    non-premultiplied fringe where partially-transparent edge pixels
    bleed the (undefined) fully-transparent-region RGB into visible
    output — which on a transparent-black canvas produces dark-blue /
    dark-purple halos around the character. We quantize to the flat
    Frontier palette immediately after, which removes any residual
    anti-aliased mid-tones and hard-restores the pixel-art look.
    """
    target_h = FEET_ANCHOR[1] + 1  # = 28
    w, h = im.size
    scale = target_h / h
    new_w = max(1, int(round(w * scale)))
    new_h = max(1, int(round(h * scale)))

    r, g, b, a = im.split()
    # Premultiply: RGB * (A / 255).
    pr = Image.eval(Image.merge("L", (r,)), lambda _v: 0)  # allocate
    src_px = im.load()
    pm = Image.new("RGBA", im.size, (0, 0, 0, 0))
    pm_px = pm.load()
    for y in range(h):
        for x in range(w):
            cr, cg, cb, ca = src_px[x, y]
            if ca == 0:
                pm_px[x, y] = (0, 0, 0, 0)
            else:
                f = ca / 255.0
                pm_px[x, y] = (int(cr * f), int(cg * f), int(cb * f), ca)
    pm_small = pm.resize((new_w, new_h), Image.LANCZOS)

    # Un-premultiply.
    out = Image.new("RGBA", pm_small.size, (0, 0, 0, 0))
    out_px = out.load()
    small_px = pm_small.load()
    for y in range(new_h):
        for x in range(new_w):
            cr, cg, cb, ca = small_px[x, y]
            if ca == 0:
                out_px[x, y] = (0, 0, 0, 0)
            else:
                f = 255.0 / ca
                out_px[x, y] = (
                    min(255, int(cr * f)),
                    min(255, int(cg * f)),
                    min(255, int(cb * f)),
                    ca,
                )
    return out


def _snap_alpha(im: Image.Image) -> Image.Image:
    """Force alpha to be strictly 0 or 255 (no anti-aliased partials)."""
    r, g, b, a = im.split()
    a = a.point(lambda v: 255 if v >= ALPHA_CUTOFF else 0)
    return Image.merge("RGBA", (r, g, b, a))


def _quantize_to_palette(im: Image.Image) -> Image.Image:
    """Snap every opaque pixel to its nearest palette color.

    Fully transparent pixels are preserved untouched. Slightly-transparent
    pixels have already been snapped by `_snap_alpha`, so we only see
    alpha in {0, 255} here.
    """
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                px[x, y] = (0, 0, 0, 0)
                continue
            # Nearest-neighbor in RGB (weighted-Euclidean would be
            # marginal here; palette is small and already well spread).
            best = PALETTE[0]
            best_d = 1 << 30
            for pr, pg, pb in PALETTE:
                dr = r - pr
                dg = g - pg
                db = b - pb
                d = dr * dr + dg * dg + db * db
                if d < best_d:
                    best_d = d
                    best = (pr, pg, pb)
            px[x, y] = (best[0], best[1], best[2], 255)
    return im


def _find_feet_row(im: Image.Image) -> int:
    """Return the last row (largest y) with any opaque pixel."""
    px = im.load()
    w, h = im.size
    for y in range(h - 1, -1, -1):
        for x in range(w):
            if px[x, y][3] != 0:
                return y
    return h - 1


def _find_center_x(im: Image.Image, feet_row: int) -> int:
    """Estimate the horizontal centerline at the feet row.

    Uses the horizontal midpoint of opaque pixels in the bottom band
    (feet_row and the two rows above), which places the character over
    its stance rather than over an off-center coat tail.
    """
    px = im.load()
    w, h = im.size
    xs: list[int] = []
    for y in range(max(0, feet_row - 2), feet_row + 1):
        for x in range(w):
            if px[x, y][3] != 0:
                xs.append(x)
    if not xs:
        return w // 2
    return (min(xs) + max(xs)) // 2


def _place_on_canvas(sprite: Image.Image, canvas_size: tuple[int, int], feet: tuple[int, int]) -> Image.Image:
    """Paste `sprite` onto a transparent canvas so its feet land at `feet`.

    "Feet" of the sprite = midpoint-x of the bottom band, last opaque row.
    """
    canvas = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    feet_row = _find_feet_row(sprite)
    center_x = _find_center_x(sprite, feet_row)

    # Offset so the sprite's (center_x, feet_row) maps to `feet` on canvas.
    ox = feet[0] - center_x
    oy = feet[1] - feet_row

    # Clip: if sprite extends above canvas, prefer preserving the head
    # region by shifting downward.
    if oy < -sprite.height + canvas_size[1]:
        pass  # OK, room above
    # Actually just paste and let clipping happen naturally; the fit step
    # already ensured height <= canvas height.
    canvas.paste(sprite, (ox, oy), sprite)
    return canvas


def process_source(
    source: Path,
    *,
    canvas_size: tuple[int, int] = CANVAS,
    feet: tuple[int, int] = FEET_ANCHOR,
) -> Image.Image:
    """Run the full source-to-32x32 pipeline on a single AI-generated PNG."""
    im = _load_rgba(source)
    im = _autocrop(im)
    im = _fit_into_canvas(im, canvas_size)
    im = _snap_alpha(im)
    im = _quantize_to_palette(im)
    return _place_on_canvas(im, canvas_size, feet)


# ---------------------------------------------------------------------------
# Frame derivations
# ---------------------------------------------------------------------------


def _shift(im: Image.Image, dx: int, dy: int) -> Image.Image:
    """Translate an image by (dx, dy) on a transparent canvas of same size."""
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    out.paste(im, (dx, dy), im)
    return out


def _overlay(base: Image.Image, top: Image.Image) -> Image.Image:
    out = base.copy()
    out.alpha_composite(top)
    return out


def _stamp(im: Image.Image, pixels: Sequence[tuple[int, int, tuple[int, int, int]]]) -> Image.Image:
    out = im.copy()
    px = out.load()
    w, h = out.size
    for x, y, rgb in pixels:
        if 0 <= x < w and 0 <= y < h:
            px[x, y] = (rgb[0], rgb[1], rgb[2], 255)
    return out


def _bob_upper(im: Image.Image, cutoff_y: int, dy: int) -> Image.Image:
    """Shift only the pixels above `cutoff_y` vertically by `dy`.

    Used to fake a subtle breathing bob on idle: torso + head lift by one
    pixel while feet stay planted at the anchor row.
    """
    w, h = im.size
    upper = im.crop((0, 0, w, cutoff_y))
    lower = im.crop((0, cutoff_y, w, h))
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.paste(lower, (0, cutoff_y), lower)
    out.paste(upper, (0, dy), upper)
    return out


def _leg_shift(im: Image.Image, cutoff_y: int, dx: int) -> Image.Image:
    """Shift only the pixels below `cutoff_y` horizontally by `dx`.

    Used to fake stride variation on idle sway or subtle run adjustments.
    """
    w, h = im.size
    upper = im.crop((0, 0, w, cutoff_y))
    lower = im.crop((0, cutoff_y, w, h))
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.paste(upper, (0, 0), upper)
    out.paste(lower, (dx, cutoff_y), lower)
    return out


GOLD = (0xF1, 0xB3, 0x36)
SAND = (0xEF, 0xBE, 0x5F)
BLUE_DARK = (0x36, 0x41, 0x53)
VIOLET = (0x50, 0x48, 0x65)


def _muzzle_flash(im: Image.Image) -> Image.Image:
    """Overlay a small gold/sand muzzle flash at EFFECT_ANCHOR (25, 15)."""
    ax, ay = EFFECT_ANCHOR
    stamps: list[tuple[int, int, tuple[int, int, int]]] = [
        (ax, ay, GOLD),
        (ax + 1, ay, SAND),
        (ax, ay - 1, GOLD),
        (ax, ay + 1, GOLD),
        (ax + 1, ay - 1, GOLD),
        (ax + 1, ay + 1, GOLD),
        (ax + 2, ay, SAND),
    ]
    return _stamp(im, stamps)


def _dash_streaks(im: Image.Image, phase: int) -> Image.Image:
    """Add horizontal speed streaks behind the character (upper-left)."""
    stamps: list[tuple[int, int, tuple[int, int, int]]] = []
    for i, y in enumerate((14, 17, 20)):
        x0 = 1 + phase
        length = 4 + i + phase
        for x in range(x0, min(x0 + length, 12)):
            stamps.append((x, y, BLUE_DARK))
        for x in range(x0 + 1, min(x0 + length - 1, 12)):
            stamps.append((x, y + 1, VIOLET))
    return _stamp(im, stamps)


def _guarantee_feet_anchor(im: Image.Image) -> Image.Image:
    """Ensure the feet-anchor pixel is opaque (renderer assumption)."""
    ax, ay = FEET_ANCHOR
    px = im.load()
    if px[ax, ay][3] == 0:
        # Find nearest opaque neighbor color in the bottom band and reuse it,
        # or fall back to the outline color.
        for y in range(ay, max(-1, ay - 3), -1):
            for x in range(im.size[0]):
                if px[x, y][3] != 0:
                    px[ax, ay] = px[x, y]
                    return im
        px[ax, ay] = (0x23, 0x18, 0x20, 255)
    return im


def _clip_below_feet(im: Image.Image) -> Image.Image:
    """Erase any opaque pixels below the feet anchor row so the sprite
    can't visually clip through the floor."""
    px = im.load()
    ay = FEET_ANCHOR[1]
    for y in range(ay + 1, im.size[1]):
        for x in range(im.size[0]):
            if px[x, y][3] != 0:
                px[x, y] = (0, 0, 0, 0)
    return im


# ---------------------------------------------------------------------------
# Per-state builders
# ---------------------------------------------------------------------------


def _base_for(state: str) -> Image.Image:
    """Load and pipeline-process the base pose PNG for `state`.

    If the caller provided per-frame sources (`hero_{state}_{n}.png`) we
    won't reach this function for those frames; this is the shared base.
    """
    src_name = "hero_idle_side.png" if state == "idle" else f"hero_{state}.png"
    src = SOURCE_DIR / src_name
    if not src.exists():
        raise FileNotFoundError(f"Missing AI source: {src}. Run generate_image.py first.")
    return process_source(src)


def _per_frame_override(state: str, frame: int) -> Image.Image | None:
    """If a per-frame source exists, prefer it over the derived variant."""
    candidate = SOURCE_DIR / f"hero_{state}_{frame}.png"
    if candidate.exists():
        return process_source(candidate)
    return None


def build_frame(state: str, frame: int) -> Image.Image:
    override = _per_frame_override(state, frame)
    if override is not None:
        return _guarantee_feet_anchor(_clip_below_feet(override))

    base = _base_for(state)

    if state == "idle":
        bobs = (0, -1, 0, 0)
        out = _bob_upper(base, cutoff_y=22, dy=bobs[frame])
    elif state == "run":
        # Stride cycle. Shift the whole body vertically by a bob and shear
        # the legs horizontally for stride illusion.
        bob_seq = (0, -1, 0, 0, -1, 0)
        leg_dx_seq = (-1, 0, +1, +2, 0, -1)
        out = _shift(base, 0, bob_seq[frame])
        out = _leg_shift(out, cutoff_y=22, dx=leg_dx_seq[frame])
    elif state == "jump":
        bob_seq = (0, -1)
        out = _shift(base, 0, bob_seq[frame])
    elif state == "fall":
        # Small hat flutter accent on frame 0 (a stray outline pixel above-left).
        out = _shift(base, 0, 0 if frame == 0 else 1)
        if frame == 0:
            out = _stamp(out, [(6, 5, (0x23, 0x18, 0x20)), (5, 6, (0x23, 0x18, 0x20))])
    elif state == "shoot":
        # 0: pre-shot. 1: muzzle flash bloom. 2: recoil kick (body pulled back and up 1px).
        if frame == 0:
            out = base
        elif frame == 1:
            out = _muzzle_flash(base)
        else:
            out = _shift(base, -1, -1)
    elif state == "reload":
        # Cycle small vertical bobs on the upper body to sell hand-work.
        bobs = (0, -1, 0, -1)
        out = _bob_upper(base, cutoff_y=22, dy=bobs[frame])
    elif state == "hurt":
        if frame == 0:
            out = _shift(base, -2, 0)  # jolted back-left on hit
        else:
            out = _shift(base, -1, 1)  # settle downward
    elif state == "dash":
        # Base + streaks; streaks cycle position for animated motion.
        out = _dash_streaks(base, phase=frame)
    else:
        raise ValueError(f"Unknown state: {state}")

    out = _clip_below_feet(out)
    out = _guarantee_feet_anchor(out)
    return out


# ---------------------------------------------------------------------------
# Validation
# ---------------------------------------------------------------------------


def validate_frame(im: Image.Image, state: str, frame: int) -> list[str]:
    problems: list[str] = []
    if im.size != CANVAS:
        problems.append(f"expected {CANVAS} got {im.size}")
    if im.mode != "RGBA":
        problems.append(f"expected RGBA got {im.mode}")
    ax, ay = FEET_ANCHOR
    if im.getpixel((ax, ay))[3] == 0:
        problems.append(f"feet anchor ({ax},{ay}) is transparent")
    # nothing below feet row
    px = im.load()
    for y in range(ay + 1, CANVAS[1]):
        for x in range(CANVAS[0]):
            if px[x, y][3] != 0:
                problems.append(f"opaque pixel below feet at ({x},{y})")
                break
        else:
            continue
        break
    # silhouette: at least ~40 opaque pixels so the character actually reads
    opaque = sum(1 for y in range(CANVAS[1]) for x in range(CANVAS[0]) if px[x, y][3] != 0)
    if opaque < 40:
        problems.append(f"silhouette too sparse ({opaque} opaque pixels)")
    return problems


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------


def _write(im: Image.Image, state: str, frame: int) -> Path:
    TARGET_DIR.mkdir(parents=True, exist_ok=True)
    dest = TARGET_DIR / f"{state}_{frame}.png"
    im.save(dest, optimize=False, compress_level=9)
    return dest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--state", default=None, help="Only process this state (idle/run/jump/fall/shoot/reload/hurt/dash).")
    parser.add_argument("--frame", type=int, default=None, help="Only process this frame within --state.")
    args = parser.parse_args()

    states = [args.state] if args.state else list(STATE_COUNTS)

    failures = 0
    for state in states:
        if state not in STATE_COUNTS:
            raise SystemExit(f"Unknown state: {state}")
        frames = [args.frame] if args.frame is not None else list(range(STATE_COUNTS[state]))
        for frame in frames:
            im = build_frame(state, frame)
            problems = validate_frame(im, state, frame)
            dest = _write(im, state, frame)
            status = "OK" if not problems else "WARN"
            print(f"[{status}] {dest.relative_to(REPO_ROOT)}", end="")
            if problems:
                failures += 1
                print(f"  <- {'; '.join(problems)}")
            else:
                print()
    if failures:
        raise SystemExit(f"{failures} frame(s) failed validation")


if __name__ == "__main__":
    main()
