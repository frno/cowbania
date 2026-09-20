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

# Target number of distinct palette colors per final frame. Hand-authored
# Frontier art (e.g. Bandit patrol) uses ~5-7 colors per 16x16 pose; we
# aim slightly higher because our canvas is 32x32 (4x the pixels) but
# still deliberately low so shapes read as flat blocks, not dither.
TARGET_COLORS_PER_FRAME = 6

# Intermediate resolution used between the premultiplied BOX downscale
# and the final mode-downscale. Chosen to be a small integer multiple of
# the final canvas (~4x) so each destination pixel corresponds to a
# roughly 4x4 patch of already-palette-snapped source pixels, which gives
# the majority-filter step enough samples to pick a stable winning color
# per output cell without smearing detail.
INTERMEDIATE_SCALE = 4

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


def _flood_fill_background(im: Image.Image) -> Image.Image:
    """Remove non-character background via corner-seeded flood fill.

    Different image models return different transparency previews baked
    into RGB — some send solid near-white, some a light-grey checkerboard,
    some a dark-grey checkerboard, some a tinted one. We can't rely on
    a fixed brightness threshold. Instead we sample each corner's actual
    color and flood outward, treating any pixel that is (a) low-saturation
    AND (b) close in luminance to a seeded corner color as background.
    That crosses both checker squares regardless of brightness but stops
    at the character's dark outline (saturated OR luminance far from
    seeds) and at saturated regions like the red bandana.
    """
    im = im.convert("RGBA")
    w, h = im.size
    px = im.load()

    seeds: list[int] = []
    for cx, cy in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        r, g, b, a = px[cx, cy]
        if a == 0:
            continue
        mx, mn = max(r, g, b), min(r, g, b)
        if (mx - mn) <= 25:
            seeds.append(mx)
    if not seeds:
        return im
    seed_min = max(0, min(seeds) - 40)
    seed_max = min(255, max(seeds) + 40)

    def is_bg(x: int, y: int) -> bool:
        r, g, b, a = px[x, y]
        if a == 0:
            return True
        mx, mn = max(r, g, b), min(r, g, b)
        if (mx - mn) > 25:
            return False
        return seed_min <= mx <= seed_max

    visited = bytearray(w * h)
    stack: list[tuple[int, int]] = []
    for cx, cy in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        if is_bg(cx, cy):
            stack.append((cx, cy))

    while stack:
        x, y = stack.pop()
        idx = y * w + x
        if visited[idx]:
            continue
        if not is_bg(x, y):
            continue
        visited[idx] = 1
        r, g, b, _ = px[x, y]
        px[x, y] = (r, g, b, 0)
        if x > 0:
            stack.append((x - 1, y))
        if x < w - 1:
            stack.append((x + 1, y))
        if y > 0:
            stack.append((x, y - 1))
        if y < h - 1:
            stack.append((x, y + 1))

    # Second pass — global sweep. Some checkerboard tiles get enclosed by
    # opaque character pixels (e.g. between an arm and the torso) and the
    # corner-seeded flood can't reach them. Any remaining pixel that
    # matches the seed criteria (low saturation AND luminance in the
    # corner-seeded window) is background regardless of connectivity.
    # This is safe as long as the character's own low-sat pixels
    # (dark outlines, shadows) don't overlap the seed luminance window,
    # which holds for every model observed so far (character outlines
    # are far darker than any transparency-preview brightness).
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            mx, mn = max(r, g, b), min(r, g, b)
            if (mx - mn) > 25:
                continue
            if seed_min <= mx <= seed_max:
                px[x, y] = (r, g, b, 0)
    return im


def _load_rgba(path: Path) -> Image.Image:
    im = Image.open(path).convert("RGBA")
    # First: flood-fill from the four corners to remove non-character BG
    # (handles checkerboard transparency previews AND solid near-white BG
    # AND textured light BGs, without accidentally erasing dark character
    # outlines or saturated character colors).
    im = _flood_fill_background(im)
    # Then: mop up any interior near-white / near-grey pixels the flood
    # fill couldn't reach (e.g. a bright highlight surrounded by dark
    # outline). Kept narrower (>= 230) than the flood-fill rule so we
    # don't hollow out the character's own light shading.
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
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


def _premultiplied_box_resize(im: Image.Image, new_w: int, new_h: int) -> Image.Image:
    """BOX-filter resize on premultiplied RGBA, then un-premultiply.

    Premultiplying avoids the classic fringe where fully-transparent RGB
    (which is undefined) bleeds into edge pixels during the filter —
    which on a transparent canvas produces dark halos. We use BOX (area
    averaging) rather than LANCZOS because BOX doesn't overshoot or
    invent mid-tone gradients around edges; both flat regions and hard
    silhouette edges survive the downscale much cleaner.
    """
    w, h = im.size
    src_px = im.load()
    pm = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    pm_px = pm.load()
    for y in range(h):
        for x in range(w):
            cr, cg, cb, ca = src_px[x, y]
            if ca == 0:
                pm_px[x, y] = (0, 0, 0, 0)
            else:
                f = ca / 255.0
                pm_px[x, y] = (int(cr * f), int(cg * f), int(cb * f), ca)
    pm_small = pm.resize((new_w, new_h), Image.BOX)
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


def _nearest_palette_index(r: int, g: int, b: int) -> int:
    best_i = 0
    best_d = 1 << 30
    for i, (pr, pg, pb) in enumerate(PALETTE):
        dr = r - pr
        dg = g - pg
        db = b - pb
        d = dr * dr + dg * dg + db * db
        if d < best_d:
            best_d = d
            best_i = i
    return best_i


def _snap_all_to_palette(im: Image.Image) -> Image.Image:
    """In-place snap of every opaque pixel to nearest Frontier palette color."""
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                px[x, y] = (0, 0, 0, 0)
                continue
            i = _nearest_palette_index(r, g, b)
            pr, pg, pb = PALETTE[i]
            px[x, y] = (pr, pg, pb, 255)
    return im


def _mode_downscale(im: Image.Image, new_w: int, new_h: int) -> Image.Image:
    """Downscale by taking the majority (mode) palette color per output cell.

    Unlike averaging filters, this can't produce a color that wasn't
    already present in the source — so pre-quantized flat regions stay
    flat, and boundaries stay 1-pixel-crisp instead of dissolving into
    anti-aliased mid-tones that later re-quantize into speckle.

    Alpha per output cell: opaque iff a majority of the sampled source
    pixels are opaque. Ties break toward transparent, which trims fringe.
    """
    src_w, src_h = im.size
    src_px = im.load()
    out = Image.new("RGBA", (new_w, new_h), (0, 0, 0, 0))
    out_px = out.load()
    for oy in range(new_h):
        y0 = (oy * src_h) // new_h
        y1 = max(y0 + 1, ((oy + 1) * src_h) // new_h)
        for ox in range(new_w):
            x0 = (ox * src_w) // new_w
            x1 = max(x0 + 1, ((ox + 1) * src_w) // new_w)
            counts: dict[tuple[int, int, int], int] = {}
            opaque = 0
            total = 0
            for yy in range(y0, y1):
                for xx in range(x0, x1):
                    r, g, b, a = src_px[xx, yy]
                    total += 1
                    if a == 0:
                        continue
                    opaque += 1
                    key = (r, g, b)
                    counts[key] = counts.get(key, 0) + 1
            if opaque * 2 <= total or not counts:
                out_px[ox, oy] = (0, 0, 0, 0)
                continue
            best_color, _ = max(counts.items(), key=lambda kv: kv[1])
            out_px[ox, oy] = (best_color[0], best_color[1], best_color[2], 255)
    return out


def _mode_filter_3x3(im: Image.Image) -> Image.Image:
    """Kill salt-and-pepper speckle by replacing isolated pixels with their
    neighborhood majority.

    For each opaque pixel: if none of its 4-connected neighbors share its
    color AND some other color dominates the 3x3 neighborhood, replace
    it with the dominant color. Transparent pixels only flip to opaque
    if strongly surrounded by opaque neighbors (>=6 of 8), which fills
    tiny pinholes without inflating the silhouette. This is deliberately
    conservative — we want to smooth speckle without eroding hats, gun
    barrels, or single-pixel accents that a good pixel artist would
    place intentionally.
    """
    src = im.copy()
    src_px = src.load()
    out_px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = src_px[x, y]
            # gather 3x3 neighborhood
            counts: dict[tuple[int, int, int], int] = {}
            opaque_neighbors = 0
            same_4neighbor = 0
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    nx, ny = x + dx, y + dy
                    if not (0 <= nx < w and 0 <= ny < h):
                        continue
                    if dx == 0 and dy == 0:
                        continue
                    nr, ng, nb, na = src_px[nx, ny]
                    if na == 0:
                        continue
                    opaque_neighbors += 1
                    key = (nr, ng, nb)
                    counts[key] = counts.get(key, 0) + 1
                    if abs(dx) + abs(dy) == 1 and a != 0 and key == (r, g, b):
                        same_4neighbor += 1
            if a == 0:
                if opaque_neighbors >= 6 and counts:
                    dom, dc = max(counts.items(), key=lambda kv: kv[1])
                    if dc >= 5:
                        out_px[x, y] = (dom[0], dom[1], dom[2], 255)
                continue
            # opaque: is this pixel an isolated/near-isolated island of color?
            # Replace when at most one 4-neighbor shares the color AND some
            # other color dominates the 3x3 neighborhood by a wide margin.
            if same_4neighbor <= 1 and counts:
                dom, dc = max(counts.items(), key=lambda kv: kv[1])
                own = counts.get((r, g, b), 0)
                if dom != (r, g, b) and dc >= 3 and dc >= own + 2:
                    out_px[x, y] = (dom[0], dom[1], dom[2], 255)
    return im


def _reduce_to_dominant_colors(im: Image.Image, target_count: int) -> Image.Image:
    """Cap the distinct opaque color count by remapping the rarest colors
    to the nearest of the top-N most common colors.

    Hand-authored Frontier art uses ~5-8 colors per sprite; forcing our
    output into the same budget is what actually makes it look
    deliberate rather than photographic — dozens of shades imply
    smoothness, few implies flat blocks. Remap is always toward an
    existing dominant color (never to a brand-new hue), so the palette
    contract with existing art is preserved.
    """
    px = im.load()
    w, h = im.size
    counts: dict[tuple[int, int, int], int] = {}
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            key = (r, g, b)
            counts[key] = counts.get(key, 0) + 1
    if len(counts) <= target_count:
        return im
    ranked = sorted(counts.items(), key=lambda kv: kv[1], reverse=True)
    keep = [c for c, _ in ranked[:target_count]]
    remap: dict[tuple[int, int, int], tuple[int, int, int]] = {}
    for color, _ in ranked[target_count:]:
        best = keep[0]
        best_d = 1 << 30
        for cand in keep:
            dr = color[0] - cand[0]
            dg = color[1] - cand[1]
            db = color[2] - cand[2]
            d = dr * dr + dg * dg + db * db
            if d < best_d:
                best_d = d
                best = cand
        remap[color] = best
    if not remap:
        return im
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            key = (r, g, b)
            if key in remap:
                nr, ng, nb = remap[key]
                px[x, y] = (nr, ng, nb, 255)
    return im


def _erase_tiny_islands(im: Image.Image, min_region_size: int = 4) -> Image.Image:
    """Erase any opaque 8-connected component smaller than `min_region_size`.

    AI-generated source images often contain incidental dust motes,
    smoke particles, or sand grains around action poses that our
    chroma-key can't catch (they aren't near-white, so they survive to
    palette-snap as opaque flecks of tan/rust across the whole canvas).
    This trims them without touching the main silhouette. 8-connected so
    that a legitimate coat-tail or gun barrel joined to the body by a
    single diagonal pixel isn't accidentally severed. Threshold of 4 is
    small enough to preserve intentional single-pixel design accents on
    the character (e.g. a hat feather touching the body) since those
    live inside larger regions, but large enough to kill true speckle.
    """
    px = im.load()
    w, h = im.size
    seen = [[False] * w for _ in range(h)]
    to_erase: list[tuple[int, int]] = []
    for sy in range(h):
        for sx in range(w):
            if seen[sy][sx] or px[sx, sy][3] == 0:
                continue
            stack = [(sx, sy)]
            component: list[tuple[int, int]] = []
            while stack:
                x, y = stack.pop()
                if not (0 <= x < w and 0 <= y < h):
                    continue
                if seen[y][x] or px[x, y][3] == 0:
                    continue
                seen[y][x] = True
                component.append((x, y))
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        if dx == 0 and dy == 0:
                            continue
                        stack.append((x + dx, y + dy))
            if len(component) < min_region_size:
                to_erase.extend(component)
    for x, y in to_erase:
        px[x, y] = (0, 0, 0, 0)
    return im


def _fit_into_canvas(im: Image.Image, canvas: tuple[int, int]) -> Image.Image:
    """Downscale a clean high-res AI image into a game-ready flat pixel-art
    frame that fits height-first into the target canvas.

    Multi-step flow (the whole point of this rewrite):

      1. Compute final target size from height fit (feet-anchor + 1 rows).
      2. Premultiplied BOX downscale to an intermediate size (~4x target)
         — collapses gradients into local averages without haloing.
      3. Alpha-snap so we only have opaque or transparent from here on.
      4. Palette-snap every opaque intermediate pixel to the nearest
         Frontier color — creates genuine flat color regions BEFORE we
         hit the final low resolution. This is the key fix vs. the old
         pipeline, which quantized only after downscale and therefore
         preserved anti-aliasing noise as per-pixel speckle.
      5. Mode-downscale to the final target size: each output cell is
         the majority color of its ~4x4 source patch. Since the source
         patch is already flat-colored, output cells are also flat, and
         boundaries stay 1-pixel-crisp.
      6. 3x3 mode-filter cleanup at target size: kill isolated speckle,
         fill tiny pinholes.
      7. Reduce to the top-N (~8) dominant colors so the frame reads as
         deliberate blocks, not a low-res photo.
    """
    target_h = FEET_ANCHOR[1] + 1  # 28
    w, h = im.size
    scale = target_h / h
    final_w = max(1, int(round(w * scale)))
    final_h = max(1, int(round(h * scale)))

    inter_w = max(final_w, final_w * INTERMEDIATE_SCALE)
    inter_h = max(final_h, final_h * INTERMEDIATE_SCALE)

    im = _premultiplied_box_resize(im, inter_w, inter_h)
    im = _snap_alpha(im)
    im = _snap_all_to_palette(im)
    im = _mode_downscale(im, final_w, final_h)
    im = _mode_filter_3x3(im)
    im = _mode_filter_3x3(im)
    im = _erase_tiny_islands(im, min_region_size=4)
    im = _reduce_to_dominant_colors(im, TARGET_COLORS_PER_FRAME)
    im = _mode_filter_3x3(im)
    im = _mode_filter_3x3(im)
    im = _erase_tiny_islands(im, min_region_size=4)
    im = _boost_readability(im)
    return im


# ---------------------------------------------------------------------------
# Background-readability lift
# ---------------------------------------------------------------------------

# Palette colors we remap globally to keep the character legible against the
# actual game backgrounds (deep_distance_blue 0x36,0x41,0x53 dominates both
# hub_far/branch_far AND appears verbatim in the character's jeans, so the
# character's lower half literally disappears against the sky). Blue-grey
# 0x53,0x67,0x78 reads as jeans while being noticeably brighter than the
# background bands.
_JEANS_DARK = (0x36, 0x41, 0x53)
_JEANS_LIFT = (0x53, 0x67, 0x78)

# Outline lift: pure outline (0x23,0x18,0x20) is nearly identical to (0,0,0)
# background bands and to the dark plum shading (0x39,0x23,0x26). To keep
# the silhouette readable against dark bg, we replace outline pixels that
# sit on the OUTER edge of the character (i.e. have a transparent 8-neighbor)
# with a warm mid-brown that still reads as an outline but pops against the
# cool background. Interior outline pixels stay dark so shading isn't lost.
_OUTLINE_DARK = (0x23, 0x18, 0x20)
_OUTLINE_RIM = (0x68, 0x3C, 0x2A)  # timber — warm mid-brown that pops on cool bg


def _boost_readability(im: Image.Image) -> Image.Image:
    """Two-part readability lift applied after palette snap.

    1. Global remap of the jeans color from deep-distance-blue (identical
       to the sky background band) to blue-grey (still reads as denim but
       is ~40% brighter and clearly separates from the sky).
    2. Silhouette rim-light — outline pixels that touch a transparent
       neighbor become warm timber-brown instead of near-black, so the
       character's outer contour reads against cool dark backgrounds
       (deep_blue, dusk_purple, black). Interior outline pixels stay
       near-black so internal shading contrast is preserved.

    This is the "sprite pops against the background" pass. Any change to
    the game's background palette should trigger a re-review of these
    two mappings.
    """
    im = im.copy()
    px = im.load()
    w, h = im.size

    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            if (r, g, b) == _JEANS_DARK:
                px[x, y] = (_JEANS_LIFT[0], _JEANS_LIFT[1], _JEANS_LIFT[2], a)

    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0 or (r, g, b) != _OUTLINE_DARK:
                continue
            on_edge = False
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    if dx == 0 and dy == 0:
                        continue
                    nx, ny = x + dx, y + dy
                    if nx < 0 or nx >= w or ny < 0 or ny >= h:
                        on_edge = True
                        break
                    if px[nx, ny][3] == 0:
                        on_edge = True
                        break
                if on_edge:
                    break
            if on_edge:
                px[x, y] = (_OUTLINE_RIM[0], _OUTLINE_RIM[1], _OUTLINE_RIM[2], a)
    return im


def _quantize_to_palette(im: Image.Image) -> Image.Image:
    """No-op retained for backward source compatibility.

    Palette snapping now happens inside `_fit_into_canvas` at the
    intermediate resolution, before the final mode-downscale, so pixels
    are already flat-region palette colors by the time we place them.
    This function guards against future callers by re-snapping any stray
    non-palette pixel (e.g. from stamp overlays that used a color not in
    the palette table) without doing per-pixel work when everything is
    already snapped.
    """
    px = im.load()
    w, h = im.size
    palette_set = set(PALETTE)
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                px[x, y] = (0, 0, 0, 0)
                continue
            if (r, g, b) in palette_set:
                continue
            i = _nearest_palette_index(r, g, b)
            pr, pg, pb = PALETTE[i]
            px[x, y] = (pr, pg, pb, 255)
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

    When `dy < 0` the shift-up leaves a transparent gap row at
    `y = cutoff_y + dy` for any column where the shifted upper had an
    opaque pixel at its bottom edge but the un-shifted lower has no
    opaque pixel yet. That gap visually splits the silhouette into two
    disconnected pieces. To preserve connectivity, we bridge the gap by
    copying the shifted upper's now-bottom row down into any exposed
    empty rows — effectively stretching the last row of the upper to
    meet the top of the lower. For `dy >= 0` (no upward shift) nothing
    needs bridging.
    """
    w, h = im.size
    upper = im.crop((0, 0, w, cutoff_y))
    lower = im.crop((0, cutoff_y, w, h))
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.paste(lower, (0, cutoff_y), lower)
    out.paste(upper, (0, dy), upper)
    if dy < 0:
        px = out.load()
        src_y = cutoff_y - 1 + dy
        if 0 <= src_y < h:
            for exposed_y in range(cutoff_y + dy, cutoff_y):
                if not (0 <= exposed_y < h):
                    continue
                for x in range(w):
                    if px[x, exposed_y][3] == 0 and px[x, src_y][3] != 0:
                        px[x, exposed_y] = px[x, src_y]
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
    """Ensure the feet-anchor pixel is opaque (renderer assumption).

    When the character's natural foot doesn't reach exactly (16, 27) we
    stamp a synthetic pixel there. To avoid leaving it as a stray island,
    we then bridge it to the nearest character pixel via a two-step path:

    1. Walk upward in the anchor column while transparent, drawing the
       bridge color, until we either meet an opaque pixel or a
       side-neighbor (left/right of the column) is opaque.
    2. If after that vertical walk the topmost bridge pixel STILL has no
       opaque 8-neighbor (character is horizontally offset — e.g. the
       dash pose leans so far the body is off to one side), walk
       horizontally in that row toward the nearest opaque pixel and
       extend the bridge along it.

    Bridges are always painted in the same color as the anchor (sampled
    from the nearest character pixel above), so the bridge reads as an
    extension of the sprite's silhouette rather than a colored streak.
    """
    ax, ay = FEET_ANCHOR
    px = im.load()
    w, h = im.size

    def _nearest_color() -> tuple[int, int, int, int]:
        for y in range(ay, max(-1, ay - 6), -1):
            for x in range(w):
                if px[x, y][3] != 0:
                    return px[x, y]
        return (0x23, 0x18, 0x20, 255)

    if px[ax, ay][3] == 0:
        px[ax, ay] = _nearest_color()
    color = px[ax, ay]

    # Vertical walk up, tracking every cell we paint so we can distinguish
    # "connected to sprite" from "connected only to our own bridge".
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
        if ay - y > 5:
            break

    bridge_set = set(bridge_cells)

    def _connected_to_sprite() -> bool:
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

    if _connected_to_sprite():
        return im

    def _horizontal_bridge(from_x: int, from_y: int) -> bool:
        left = right = None
        for dx in range(1, w):
            xl = from_x - dx
            xr = from_x + dx
            if xl >= 0 and (xl, from_y) not in bridge_set and px[xl, from_y][3] != 0 and left is None:
                left = xl
            if xr < w and (xr, from_y) not in bridge_set and px[xr, from_y][3] != 0 and right is None:
                right = xr
            if left is not None or right is not None:
                break
        if left is None and right is None:
            return False
        if left is not None and right is not None:
            target = left if (from_x - left) <= (right - from_x) else right
        else:
            target = left if left is not None else right
        step = 1 if target > from_x else -1
        x = from_x + step
        while x != target:
            px[x, from_y] = color
            x += step
        return True

    if not _horizontal_bridge(top_x, top_y):
        if top_y > 0:
            _horizontal_bridge(top_x, top_y - 1)
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
        # Subtle vertical bob so the two fall frames animate slightly.
        # (Legacy per-frame stamp overlays were tuned to the previous
        # character's silhouette coordinates and read as disconnected
        # specks on the current sprite; use only a plain shift now.)
        out = _shift(base, 0, 0 if frame == 0 else 1)
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
