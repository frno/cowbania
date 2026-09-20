"""Pixelate AI-generated enemy sprites into the game's 16x16 Frontier format.

Companion to `pixelate_sprite.py` (which handles the 32x32 player). Reuses
that module's shared low-level pipeline primitives (`_load_rgba`,
`_autocrop`, `_fit_into_canvas`, `_boost_readability` etc.) but redefines
the canvas dimensions, the feet anchor, the state list, and the per-state
derivation logic for enemies.

Four enemy types, matching the stable asset contract in
`Assets/Art/Frontier/manifest.md`:

* `Bandit` — upright human gunslinger silhouette, 12 frames:
  `patrol_0..3`, `notice_0..1`, `attack_0..3`, `defeated_0..1`.
* `Wildlife` — low, wide, forward-heavy quadruped silhouette, 12 frames:
  `patrol_0..3`, `notice_0..1`, `lunge_0..3`, `defeated_0..1`.
* `Armadillo` — low rolling shell hazard, 10 frames:
  `patrol_0..3`, `notice_0..1`, `roll_0..3`.
* `Snake` — stationary ambush hazard, 12 frames:
  `hidden_0..1`, `rise_0..1`, `exposed_0..3`, `retreat_0..1`,
  `defeated_0..1`.

Anchors:
* Canvas 16x16.
* Feet anchor `(8, 15)` (bottom-center) for both types.
* Notice indicator anchor `(8, 2)` for the gold `!` (bandit) or `|`
  (wildlife) telegraph glyph — placed above the character's head.
* Muzzle anchor `(14, 8)` for the bandit's attack frame (right-facing).

Frame derivation
----------------
One AI call per state per enemy. Intra-state frames are derived by
pixel-level operations:

  bandit patrol   4 frames: bob + leg-shift walk cycle.
  bandit notice   2 frames: base + head-up variant, both with gold `!`
                            indicator above the head.
  bandit attack   4 frames: draw / fire (with muzzle-flash stamp) /
                            recoil shift / return.
  bandit defeated 2 frames: fallen base + dust-puff variant.

  wildlife patrol  4 frames: bob + leg-shift ground-hugging cycle.
  wildlife notice  2 frames: base + head-up variant with gold `|`
                             telegraph glyph above the head.
  wildlife lunge   4 frames: crouch / mid-lunge / peak with streak
                             accents / recovery.
  wildlife defeated 2 frames: fallen base + dust-puff variant.

  armadillo patrol  4 frames: bob-cycle crawl with fuse twinkle.
  armadillo notice  2 frames: stop / tense-up with brighter fuse catch.
  armadillo roll    4 frames: compact charge cycle with low speed streaks.

  snake hidden     2 frames: dirt-mound hazard marker with warning glint.
  snake rise       2 frames: emerging from the mound.
  snake exposed    4 frames: upright idle-sway / strike-ready cycle.
  snake retreat    2 frames: dropping back into the mound.
  snake defeated   2 frames: collapsed body + hit-flash variant.

Usage:
    python tools/nanogpt/pixelate_enemy.py           # all enemy frames
    python tools/nanogpt/pixelate_enemy.py --enemy bandit
    python tools/nanogpt/pixelate_enemy.py --enemy snake --state exposed
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from PIL import Image

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))
import pixelate_sprite as ps  # noqa: E402  reuse low-level primitives

REPO_ROOT = TOOLS.parents[1]
SOURCE_DIR = TOOLS / "out"
BANDIT_DIR = REPO_ROOT / "Assets" / "Art" / "Frontier" / "Bandit"
WILDLIFE_DIR = REPO_ROOT / "Assets" / "Art" / "Frontier" / "Wildlife"
ARMADILLO_DIR = REPO_ROOT / "Assets" / "Art" / "Frontier" / "Armadillo"
SNAKE_DIR = REPO_ROOT / "Assets" / "Art" / "Frontier" / "Snake"

CANVAS = (16, 16)
FEET_ANCHOR = (8, 15)
NOTICE_ANCHOR = (8, 2)          # gold indicator over head
BANDIT_MUZZLE_ANCHOR = (14, 8)  # right-facing attack muzzle

# Fewer target colors per 16x16 frame — hand-authored Frontier art uses
# ~4-5 colors on Bandit/Wildlife, and 256 pixels can't support the 6+
# used on the 32x32 player without visible speckle.
TARGET_COLORS = 4

STATE_COUNTS = {
    "bandit": {"patrol": 4, "notice": 2, "attack": 4, "defeated": 2},
    "wildlife": {"patrol": 4, "notice": 2, "lunge": 4, "defeated": 2},
    "armadillo": {"patrol": 4, "notice": 2, "roll": 4},
    "snake": {"hidden": 2, "rise": 2, "exposed": 4, "retreat": 2, "defeated": 2},
}

DEST_DIRS = {
    "bandit": BANDIT_DIR,
    "wildlife": WILDLIFE_DIR,
    "armadillo": ARMADILLO_DIR,
    "snake": SNAKE_DIR,
}

# Palette convenience (from Frontier palette in pixelate_sprite.PALETTE).
OUTLINE = (0x23, 0x18, 0x20)
DEEP_BROWN = (0x39, 0x23, 0x26)
RUST = (0x8E, 0x42, 0x2A)
OCHRE = (0xCB, 0x85, 0x36)
GOLD = (0xF1, 0xB3, 0x36)
SAND = (0xEF, 0xBE, 0x5F)
TIMBER = (0x68, 0x3C, 0x2A)
TIMBER_HI = (0xA5, 0x63, 0x34)
BONE = (0xE0, 0xCC, 0x9D)
VIOLET = (0x50, 0x48, 0x65)
BLUE_GREY = (0x53, 0x67, 0x78)
DEEP_BLUE = (0x36, 0x41, 0x53)
SAGE = (0x5C, 0x6F, 0x53)
RED = (0xBE, 0x3D, 0x30)


# ---------------------------------------------------------------------------
# 16x16-specific helpers (shadow the 32x32 versions in pixelate_sprite)
# ---------------------------------------------------------------------------


def _guarantee_feet_anchor(im: Image.Image) -> Image.Image:
    """16x16 equivalent of pixelate_sprite._guarantee_feet_anchor.

    Vertical bridge from (8,15) upward, then horizontal bridge if still
    isolated. See pixelate_sprite for the full rationale — the algorithm
    is identical, only the anchor coord differs.
    """
    ax, ay = FEET_ANCHOR
    px = im.load()
    w, h = im.size

    def _nearest_color() -> tuple[int, int, int, int]:
        for y in range(ay, max(-1, ay - 4), -1):
            for x in range(w):
                if px[x, y][3] != 0:
                    return px[x, y]
        return (OUTLINE[0], OUTLINE[1], OUTLINE[2], 255)

    if px[ax, ay][3] == 0:
        px[ax, ay] = _nearest_color()
    color = px[ax, ay]

    bridge_cells: list[tuple[int, int]] = [(ax, ay)]
    top_x, top_y = ax, ay
    y = ay - 1
    while y >= 0 and px[ax, y][3] == 0:
        px[ax, y] = color
        bridge_cells.append((ax, y))
        top_x, top_y = ax, y
        for dx in (-1, 1):
            nx = ax + dx
            if 0 <= nx < w and px[nx, y][3] != 0:
                return im
        y -= 1
        if ay - y > 4:  # smaller bridge budget on 16x16
            break

    bridge_set = set(bridge_cells)

    def _connected() -> bool:
        for bx, by in bridge_cells:
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    if dx == 0 and dy == 0:
                        continue
                    nx, ny = bx + dx, by + dy
                    if (nx, ny) in bridge_set:
                        continue
                    if 0 <= nx < w and 0 <= ny < h and px[nx, ny][3] != 0:
                        return True
        return False

    if _connected():
        return im

    def _horizontal(from_x: int, from_y: int) -> bool:
        left = right = None
        for dx in range(1, w):
            xl, xr = from_x - dx, from_x + dx
            if xl >= 0 and (xl, from_y) not in bridge_set and px[xl, from_y][3] != 0 and left is None:
                left = xl
            if xr < w and (xr, from_y) not in bridge_set and px[xr, from_y][3] != 0 and right is None:
                right = xr
            if left is not None or right is not None:
                break
        if left is None and right is None:
            return False
        target = left if (right is None or (left is not None and (from_x - left) <= (right - from_x))) else right
        step = 1 if target > from_x else -1
        x = from_x + step
        while x != target:
            px[x, from_y] = color
            x += step
        return True

    if not _horizontal(top_x, top_y):
        if top_y > 0:
            _horizontal(top_x, top_y - 1)
    return im


def _clip_below_feet(im: Image.Image) -> Image.Image:
    px = im.load()
    ay = FEET_ANCHOR[1]
    for y in range(ay + 1, im.size[1]):
        for x in range(im.size[0]):
            if px[x, y][3] != 0:
                px[x, y] = (0, 0, 0, 0)
    return im


def _place_on_canvas(sprite: Image.Image) -> Image.Image:
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    feet_row = ps._find_feet_row(sprite)
    center_x = ps._find_center_x(sprite, feet_row)
    ox = FEET_ANCHOR[0] - center_x
    oy = FEET_ANCHOR[1] - feet_row
    canvas.paste(sprite, (ox, oy), sprite)
    return canvas


def _connected_components(im: Image.Image) -> list[list[tuple[int, int]]]:
    """8-connected components of opaque pixels."""
    px = im.load()
    w, h = im.size
    seen = [[False] * w for _ in range(h)]
    comps: list[list[tuple[int, int]]] = []
    for y in range(h):
        for x in range(w):
            if seen[y][x] or px[x, y][3] == 0:
                continue
            stack = [(x, y)]
            seen[y][x] = True
            cells: list[tuple[int, int]] = []
            while stack:
                cx, cy = stack.pop()
                cells.append((cx, cy))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        if dx == 0 and dy == 0:
                            continue
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and px[nx, ny][3] != 0:
                            seen[ny][nx] = True
                            stack.append((nx, ny))
            comps.append(cells)
    return comps


def _ensure_single_component(im: Image.Image) -> Image.Image:
    """Reconnect stray fragments to the main silhouette with 1-pixel bridges.

    At 16x16 an AI source with thin legs frequently downsamples into a
    torso component + a feet component with a 1-2 row gap. A pure feet
    anchor bridge can't span that gap because the feet component *is*
    already opaque at that column. This walks every non-main component
    to the nearest main-component cell and draws a 1-pixel-thick bridge
    (vertical then horizontal) so `connected components == 1`.
    """
    px = im.load()
    w, h = im.size
    for _ in range(4):  # bounded iteration
        comps = _connected_components(im)
        if len(comps) <= 1:
            return im
        comps.sort(key=len, reverse=True)
        main = set(comps[0])
        stray = comps[1]
        # pick the stray cell closest to main
        best = None
        for sx, sy in stray:
            for mx, my in main:
                d2 = (sx - mx) ** 2 + (sy - my) ** 2
                if best is None or d2 < best[0]:
                    best = (d2, sx, sy, mx, my)
        assert best is not None
        _, sx, sy, mx, my = best
        color = px[sx, sy]
        # vertical then horizontal Manhattan bridge
        x, y = sx, sy
        while y != my:
            step = 1 if my > y else -1
            y += step
            if 0 <= y < h and px[x, y][3] == 0:
                px[x, y] = color
        while x != mx:
            step = 1 if mx > x else -1
            x += step
            if 0 <= x < w and px[x, y][3] == 0:
                px[x, y] = color
        # now stray should be joined; loop repeats until single component
    return im


def _process_source(source: Path) -> Image.Image:
    """Full source-to-16x16 pipeline for one AI-generated PNG.

    Reimplements the pixelate_sprite `_fit_into_canvas` flow with a
    `contain`-style scale (`min(canvas_w / w, canvas_h / h)`) instead of
    that module's height-first fit. Height-first would make horizontal
    subjects (the wildlife quadruped) blow past the 16-pixel width; a
    contain-fit keeps both the tall bandit and the wide wildlife on the
    canvas with the correct aspect ratio.
    """
    im = ps._load_rgba(source)
    im = ps._autocrop(im)

    target_h = FEET_ANCHOR[1] + 1  # 16
    target_w = CANVAS[0]           # 16
    w, h = im.size
    scale = min(target_w / w, target_h / h)
    final_w = max(1, int(round(w * scale)))
    final_h = max(1, int(round(h * scale)))
    inter_scale = ps.INTERMEDIATE_SCALE
    inter_w = max(final_w, final_w * inter_scale)
    inter_h = max(final_h, final_h * inter_scale)

    original_colors = ps.TARGET_COLORS_PER_FRAME
    try:
        ps.TARGET_COLORS_PER_FRAME = TARGET_COLORS
        im = ps._premultiplied_box_resize(im, inter_w, inter_h)
        im = ps._snap_alpha(im)
        im = ps._snap_all_to_palette(im)
        im = ps._mode_downscale(im, final_w, final_h)
        im = ps._mode_filter_3x3(im)
        im = ps._mode_filter_3x3(im)
        im = ps._erase_tiny_islands(im, min_region_size=3)
        im = ps._reduce_to_dominant_colors(im, TARGET_COLORS)
        im = ps._mode_filter_3x3(im)
        im = ps._erase_tiny_islands(im, min_region_size=3)
        im = ps._boost_readability(im)
    finally:
        ps.TARGET_COLORS_PER_FRAME = original_colors

    im = ps._snap_alpha(im)
    im = ps._quantize_to_palette(im)
    result = _place_on_canvas(im)
    return _ensure_single_component(result)


# ---------------------------------------------------------------------------
# Frame derivations
# ---------------------------------------------------------------------------


def _shift(im: Image.Image, dx: int, dy: int) -> Image.Image:
    return ps._shift(im, dx, dy)


def _leg_shift(im: Image.Image, cutoff_y: int, dx: int) -> Image.Image:
    return ps._leg_shift(im, cutoff_y, dx)


def _bob_upper(im: Image.Image, cutoff_y: int, dy: int) -> Image.Image:
    return ps._bob_upper(im, cutoff_y, dy)


def _stamp(im: Image.Image, pixels) -> Image.Image:
    return ps._stamp(im, pixels)


def _tighten_palette(im: Image.Image, target_colors: int = 4) -> Image.Image:
    im = im.copy()
    im = ps._reduce_to_dominant_colors(im, target_colors)
    im = ps._mode_filter_3x3(im)
    return ps._snap_alpha(im)


def _apply_palette_map(im: Image.Image, mapping: dict[tuple[int, int, int], tuple[int, int, int]]) -> Image.Image:
    im = im.copy()
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            key = (r, g, b)
            if key in mapping:
                nr, ng, nb = mapping[key]
                px[x, y] = (nr, ng, nb, a)
    return im


def _notice_bang(im: Image.Image, offset_y: int = 0) -> Image.Image:
    """Stamp a bright gold `!` above the head (bandit)."""
    ax, ay = NOTICE_ANCHOR
    ay += offset_y
    return _stamp(im, [
        (ax, ay, GOLD),
        (ax, ay + 1, GOLD),
        (ax, ay + 3, GOLD),  # dot below the bar
    ])


def _notice_bar(im: Image.Image, offset_y: int = 0) -> Image.Image:
    """Stamp a bright gold `|` above the head (wildlife)."""
    ax, ay = NOTICE_ANCHOR
    ay += offset_y
    return _stamp(im, [
        (ax, ay, GOLD),
        (ax, ay + 1, GOLD),
        (ax, ay + 2, GOLD),
    ])


def _muzzle_flash_bandit(im: Image.Image) -> Image.Image:
    """Compact 4-pixel muzzle flash at bandit's right-facing muzzle."""
    mx, my = BANDIT_MUZZLE_ANCHOR
    return _stamp(im, [
        (mx, my, GOLD),
        (mx, my - 1, SAND),
        (mx, my + 1, SAND),
    ])


def _dust_puff(im: Image.Image, side: str) -> Image.Image:
    """Small kicked-up dust puff over a fallen sprite. `side` is 'left'|'right'."""
    if side == "left":
        stamps = [
            (2, 12, BONE),
            (3, 13, SAND),
            (1, 13, SAND),
        ]
    else:
        stamps = [
            (13, 12, BONE),
            (12, 13, SAND),
            (14, 13, SAND),
        ]
    return _stamp(im, stamps)


def _lunge_streaks(im: Image.Image, phase: int) -> Image.Image:
    """Add short horizontal speed streaks BEHIND (left of) a lunging wildlife."""
    stamps = []
    for i, y in enumerate((10, 12)):
        length = 2 + phase + i
        for x in range(0, min(length, 5)):
            stamps.append((x, y, VIOLET))
    return _stamp(im, stamps)


def _armadillo_fuse(im: Image.Image, phase: int = 0, bright: bool = False) -> Image.Image:
    """Tiny ember/fuse telegraph for the armadillo shell top."""
    core = GOLD if bright else SAND
    hot = SAND if bright else GOLD
    x = 8 + ((phase % 2) - 0)
    y = 3 + (0 if bright else (phase % 2))
    stamps = [
        (x, y, core),
        (x, y - 1, hot),
        (x - 1, y, OCHRE if bright else TIMBER_HI),
    ]
    if bright:
        stamps.append((x + 1, y - 1, SAND))
    return _stamp(im, stamps)


def _low_speed_streaks(im: Image.Image, phase: int) -> Image.Image:
    """Low trailing streaks behind compact ground-hugging motion."""
    stamps = []
    for y in (11, 12, 13):
        length = min(5, 1 + phase + (1 if y == 12 else 0))
        for x in range(length):
            stamps.append((x, y, TIMBER))
    return _stamp(im, stamps)


def _hidden_glint(im: Image.Image, phase: int = 0) -> Image.Image:
    """Telegraph glint inside the snake's dirt crack marker."""
    x = 8 + (1 if phase % 2 else 0)
    return _stamp(im, [(x, 12, GOLD)])


def _snake_eye_flash(im: Image.Image, phase: int = 0) -> Image.Image:
    """Tiny gold eye flash on the snake's raised head."""
    x = 11
    y = 5 + (phase % 2)
    return _stamp(im, [(x, y, GOLD), (x - 1, y + 1, BONE)])


def _snake_mound(im: Image.Image, variant: int = 0) -> Image.Image:
    """Reinforce the dirt mound foreground read for hidden/rise/retreat states."""
    if variant == 0:
        stamps = [(4, 14, TIMBER), (5, 13, RUST), (10, 13, RUST), (11, 14, TIMBER)]
    else:
        stamps = [(3, 14, TIMBER), (5, 13, RUST), (9, 13, RUST), (12, 14, TIMBER)]
    return _stamp(im, stamps)


def _snake_hit_flash(im: Image.Image) -> Image.Image:
    """Small bullet-hit accent for the defeated snake variant."""
    return _stamp(im, [
        (11, 9, RED),
        (12, 8, BONE),
    ])


# ---------------------------------------------------------------------------
# Sources
# ---------------------------------------------------------------------------


def _source_key(enemy: str, state: str) -> Path:
    return SOURCE_DIR / f"hero_{enemy}_{state}.png"


def _base_for(enemy: str, state: str) -> Image.Image:
    """Load the AI base PNG for (enemy, state) through the pipeline."""
    src = _source_key(enemy, state)
    if not src.exists():
        raise FileNotFoundError(
            f"Missing AI source: {src}. Run generate_enemy_frames.py first."
        )
    return _process_source(src)


def _per_frame_override(enemy: str, state: str, frame: int) -> Image.Image | None:
    candidate = SOURCE_DIR / f"hero_{enemy}_{state}_{frame}.png"
    if candidate.exists():
        return _process_source(candidate)
    return None


# ---------------------------------------------------------------------------
# Per-state builders
# ---------------------------------------------------------------------------


def _build_bandit(state: str, frame: int) -> Image.Image:
    override = _per_frame_override("bandit", state, frame)
    if override is not None:
        return _guarantee_feet_anchor(_clip_below_feet(override))

    base = _base_for("bandit", state)

    if state == "patrol":
        # At 16x16 the bandit's legs are only 1-2 pixel columns wide, so the
        # 32x32-style leg_shift disconnects the feet from the body. Use a
        # simple whole-sprite bob cycle instead — reads as breathing/idle
        # sway without producing stray components.
        bob_seq = (0, -1, 0, -1)
        out = _shift(base, 0, bob_seq[frame])
    elif state == "notice":
        out = _bob_upper(base, cutoff_y=10, dy=(0, -1)[frame])
        out = _notice_bang(out, offset_y=frame)
    elif state == "attack":
        if frame == 0:
            out = base
        elif frame == 1:
            out = _muzzle_flash_bandit(base)
        elif frame == 2:
            out = _shift(base, -1, 0)
            out = _muzzle_flash_bandit(out)
        else:
            out = base
    elif state == "defeated":
        out = base if frame == 0 else _dust_puff(base, "right")
    else:
        raise ValueError(f"Unknown bandit state: {state}")

    out = _clip_below_feet(out)
    out = _guarantee_feet_anchor(out)
    return out


def _build_wildlife(state: str, frame: int) -> Image.Image:
    override = _per_frame_override("wildlife", state, frame)
    if override is not None:
        return _guarantee_feet_anchor(_clip_below_feet(override))

    base = _base_for("wildlife", state)

    if state == "patrol":
        bob_seq = (0, -1, 0, 0)
        leg_seq = (-1, 0, +1, 0)
        out = _shift(base, 0, bob_seq[frame])
        out = _leg_shift(out, cutoff_y=12, dx=leg_seq[frame])
    elif state == "notice":
        out = _bob_upper(base, cutoff_y=10, dy=(0, -1)[frame])
        out = _notice_bar(out, offset_y=frame)
    elif state == "lunge":
        if frame == 0:
            out = _shift(base, -1, 0)  # crouch back
        elif frame == 1:
            out = base
        elif frame == 2:
            out = _lunge_streaks(_shift(base, +1, 0), phase=1)
        else:
            out = _lunge_streaks(_shift(base, +2, 0), phase=2)
    elif state == "defeated":
        out = base if frame == 0 else _dust_puff(base, "left")
    else:
        raise ValueError(f"Unknown wildlife state: {state}")

    out = _clip_below_feet(out)
    out = _guarantee_feet_anchor(out)
    return out


def _build_armadillo(state: str, frame: int) -> Image.Image:
    override = _per_frame_override("armadillo", state, frame)
    if override is not None:
        return _guarantee_feet_anchor(_clip_below_feet(override))

    base = _base_for("armadillo", "patrol" if state == "notice" else state)

    if state == "patrol":
        bob_seq = (0, -1, 0, -1)
        sway_seq = (0, 1, 0, -1)
        out = _shift(base, sway_seq[frame], bob_seq[frame])
        out = _tighten_palette(out, 4)
        out = _armadillo_fuse(out, phase=frame, bright=False)
    elif state == "notice":
        out = _bob_upper(base, cutoff_y=8, dy=(0, -1)[frame])
        out = _tighten_palette(out, 4)
        out = _armadillo_fuse(out, phase=frame, bright=True)
    elif state == "roll":
        x_seq = (-1, 0, 1, 0)
        y_seq = (0, -1, 0, -1)
        out = _shift(base, x_seq[frame], y_seq[frame])
        out = _tighten_palette(out, 4)
        out = _armadillo_fuse(out, phase=frame, bright=True)
        if frame >= 1:
            out = _low_speed_streaks(out, phase=frame)
    else:
        raise ValueError(f"Unknown armadillo state: {state}")

    out = _apply_palette_map(out, {
        TIMBER_HI: RUST,
        SAND: GOLD,
        RED: RUST,
        BONE: OCHRE,
    })
    out = _clip_below_feet(out)
    out = _guarantee_feet_anchor(out)
    return out


def _build_snake(state: str, frame: int) -> Image.Image:
    override = _per_frame_override("snake", state, frame)
    if override is not None:
        return _guarantee_feet_anchor(_clip_below_feet(override))

    base = _base_for("snake", state)

    if state == "hidden":
        out = _tighten_palette(base, 4)
        if frame != 0:
            out = _hidden_glint(out, phase=frame)
        out = _snake_mound(out, variant=frame)
    elif state == "rise":
        out = _tighten_palette(_shift(base, 0, (1, 0)[frame]), 4)
        out = _snake_mound(out, variant=frame)
        if frame == 1:
            out = _snake_eye_flash(out, phase=frame)
    elif state == "exposed":
        sway_x = (0, 1, 0, -1)
        neck_dy = (0, -1, 0, -1)
        out = _shift(base, sway_x[frame], 0)
        out = _bob_upper(out, cutoff_y=9, dy=neck_dy[frame])
        out = _tighten_palette(out, 4)
        if frame in (1, 3):
            out = _snake_eye_flash(out, phase=frame)
    elif state == "retreat":
        out = _tighten_palette(_shift(base, 0, (0, 1)[frame]), 4)
        out = _snake_mound(out, variant=frame)
    elif state == "defeated":
        out = _tighten_palette(base if frame == 0 else _shift(base, 1, 0), 4)
        if frame == 1:
            out = _snake_hit_flash(out)
    else:
        raise ValueError(f"Unknown snake state: {state}")

    out = _clip_below_feet(out)
    out = _guarantee_feet_anchor(out)
    return out


def build_frame(enemy: str, state: str, frame: int) -> Image.Image:
    if enemy == "bandit":
        return _build_bandit(state, frame)
    if enemy == "wildlife":
        return _build_wildlife(state, frame)
    if enemy == "armadillo":
        return _build_armadillo(state, frame)
    if enemy == "snake":
        return _build_snake(state, frame)
    raise ValueError(f"Unknown enemy: {enemy}")


# ---------------------------------------------------------------------------
# Output
# ---------------------------------------------------------------------------


def _write(im: Image.Image, enemy: str, state: str, frame: int) -> Path:
    dest_dir = DEST_DIRS[enemy]
    dest_dir.mkdir(parents=True, exist_ok=True)
    dest = dest_dir / f"{state}_{frame}.png"
    im.save(dest)
    return dest


def validate_frame(im: Image.Image, state: str, frame: int) -> list[str]:
    problems: list[str] = []
    if im.size != CANVAS:
        problems.append(f"canvas is {im.size}, expected {CANVAS}")
    ax, ay = FEET_ANCHOR
    px = im.load()
    if px[ax, ay][3] == 0:
        problems.append(f"feet anchor ({ax},{ay}) is transparent")
    for y in range(ay + 1, im.size[1]):
        for x in range(im.size[0]):
            if px[x, y][3] != 0:
                problems.append(f"opaque pixel below feet anchor at ({x},{y})")
                return problems
    # transparent-bg contract (Host test requires MinimumAlpha == 0)
    has_transparent = False
    for y in range(im.size[1]):
        for x in range(im.size[0]):
            if px[x, y][3] == 0:
                has_transparent = True
                break
        if has_transparent:
            break
    if not has_transparent:
        problems.append("no transparent pixels — fails MinimumAlpha==0 contract")
    return problems


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--enemy", choices=tuple(STATE_COUNTS), default=None)
    parser.add_argument("--state", default=None)
    parser.add_argument("--frame", type=int, default=None)
    args = parser.parse_args()

    enemies = [args.enemy] if args.enemy else ["bandit", "wildlife"]
    failures = 0
    for enemy in enemies:
        states = [args.state] if args.state else list(STATE_COUNTS[enemy])
        for state in states:
            if state not in STATE_COUNTS[enemy]:
                raise SystemExit(f"Unknown state for {enemy}: {state}")
            frames = [args.frame] if args.frame is not None else list(range(STATE_COUNTS[enemy][state]))
            for frame in frames:
                im = build_frame(enemy, state, frame)
                problems = validate_frame(im, state, frame)
                dest = _write(im, enemy, state, frame)
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
