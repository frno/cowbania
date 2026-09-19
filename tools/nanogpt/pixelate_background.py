"""Pixelate AI-generated background sources into game-ready 256x144 tileable PNGs.

Companion to `pixelate_sprite.py` (32x32 player) and `pixelate_enemy.py`
(16x16 enemies). Backgrounds are the third and final actor-scale pipeline
in the Cowbania AI-art stack.

Stable asset contract (see `Assets/Art/Frontier/manifest.md`):

* 256x144 RGBA.
* Two parallax layers per stage: `hub_{far,mid}`, `branch_{far,mid}`.
* Horizontally tileable — the game's `StageRenderer.DrawBackgroundBand`
  wraps by texture width for scrolling parallax. A visible seam at the
  wrap point would ruin the illusion.
* Two colors per layer (sky + silhouette). Sky pixels are TRANSPARENT
  (alpha=0) so the game can clear to any base color underneath.
* Low saturation / low contrast — backgrounds must RECEDE, not compete
  with the actor foreground. Palette entries come from
  `pixelate_sprite.PALETTE` (Frontier), namely:
    - far  layers: sky `distance_violet 0x504865`, silhouette
      `blue_grey 0x536778`
    - mid  layers: sky `dusk_plum 0x39,0x23,0x26` (darker), silhouette
      `dusk_plum_hi 0x644c5b`
  Palette values are matched against `pixelate_sprite.PALETTE` for
  consistency with the actor pipeline.

Pipeline stages
---------------

  1. Load AI source (16:9 landscape from `generate_background_frames.py`).
  2. LANCZOS-downscale straight to 256x144. Backgrounds don't need the
     multi-stage box→mode-downscale flow the actors use — they're
     already silhouette-style flat regions at the source, and the
     lower-frequency details survive a direct downscale cleanly.
  3. Quantize luminance to 2 levels: sky (brighter/upper) vs silhouette
     (darker/lower). Uses Otsu-ish midpoint of the luminance histogram
     picked from the top-row and bottom-row samples so the split is
     robust to source variation.
  4. Chroma-key the sky level to fully transparent alpha.
  5. Snap the silhouette level to the assigned Frontier palette color
     for this layer.
  6. Horizontal seam-repair: cross-blend the leftmost/rightmost N
     columns' silhouette HEIGHT MAPS so `im.paste(im, (256, 0))` is
     visually seamless when the game wraps. Silhouettes are cheap to
     restitch because we only need to reconcile the "topmost silhouette
     row per column" — not a full pixel field.
  7. Validate: exact canvas size, at least some transparent pixels
     (sky), silhouette fully snapped to expected palette hex.

Usage:
    python tools/nanogpt/pixelate_background.py         # all 4
    python tools/nanogpt/pixelate_background.py --layer hub_far
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from PIL import Image

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))
import pixelate_sprite as ps  # noqa: E402

REPO_ROOT = TOOLS.parents[1]
SOURCE_DIR = TOOLS / "out"
DEST_DIR = REPO_ROOT / "Assets" / "Art" / "Frontier" / "Background"

CANVAS = (256, 144)
SEAM_BLEND = 32  # columns on each side blended for horizontal wrap

# Layer -> (sky_rgb, silhouette_rgb). Sky becomes fully transparent in
# output, but the sampled luminance target drives the 2-level quantize
# so the AI's actual sky level maps to the correct half of the split.
LAYER_PALETTES: dict[str, tuple[tuple[int, int, int], tuple[int, int, int]]] = {
    "hub_far": ((0x50, 0x48, 0x65), (0x53, 0x67, 0x78)),
    "hub_mid": ((0x39, 0x23, 0x26), (0x64, 0x4c, 0x5b)),
    "branch_far": ((0x50, 0x48, 0x65), (0x53, 0x67, 0x78)),
    "branch_mid": ((0x39, 0x23, 0x26), (0x64, 0x4c, 0x5b)),
}


def _luminance(rgb: tuple[int, int, int]) -> float:
    r, g, b = rgb
    return 0.299 * r + 0.587 * g + 0.114 * b


def _quantize_source_to_two(im: Image.Image) -> Image.Image:
    """Reduce the source AI image to exactly 2 colors (sky + silhouette)
    at full source resolution, so small features aren't averaged into
    the sky during downscale.

    PIL's median-cut with `colors=2` is more color-aware than a pure
    luminance threshold: it clusters the RGB space and picks the two
    dominant palette entries the AI actually produced (usually the
    prompt's requested sky + silhouette colors).
    """
    p = im.convert("RGB").quantize(colors=2, method=Image.MEDIANCUT).convert("RGB")
    return p


def _mode_downscale(im: Image.Image, size: tuple[int, int]) -> Image.Image:
    """Majority-color downscale — reuses pixelate_sprite's _mode_downscale.

    Each destination pixel is the mode of its source patch. Since the
    source has already been reduced to 2 colors, each destination pixel
    is either sky or silhouette; there's no color blending. This
    preserves narrow features (a single tall silhouette-colored column
    in the source is a full silhouette column in the output).
    """
    return ps._mode_downscale(im.convert("RGBA"), size[0], size[1])


def _pick_split_threshold(im: Image.Image) -> float:
    """Choose a luminance threshold that separates sky from silhouette
    using Otsu's method on the full-image luminance histogram.

    Sampling only top/bottom bands is fragile: the very bottom rows of
    a "far" layer often contain SKY between the ground line and the
    tiny distant-building silhouettes, so the "bottom sample" is
    contaminated. Otsu's max-between-class-variance split finds the
    real bimodal midpoint from the whole distribution — robust for any
    2-color-target source the AI happens to produce.
    """
    px = im.load()
    w, h = im.size
    hist = [0] * 256
    total = 0
    for y in range(h):
        for x in range(w):
            lum = int(_luminance(px[x, y][:3]))
            lum = max(0, min(255, lum))
            hist[lum] += 1
            total += 1
    sum_all = sum(i * hist[i] for i in range(256))
    sum_b = 0.0
    w_b = 0
    max_var = -1.0
    threshold = 128.0
    for i in range(256):
        w_b += hist[i]
        if w_b == 0:
            continue
        w_f = total - w_b
        if w_f == 0:
            break
        sum_b += i * hist[i]
        m_b = sum_b / w_b
        m_f = (sum_all - sum_b) / w_f
        var = w_b * w_f * (m_b - m_f) ** 2
        if var > max_var:
            max_var = var
            threshold = float(i)
    return threshold


def _two_level_quantize(
    im: Image.Image,
    sky_rgb: tuple[int, int, int],
    silhouette_rgb: tuple[int, int, int],
) -> Image.Image:
    """Map the two source palette entries (already exactly-2-color from
    `_quantize_source_to_two`) to sky (transparent) and silhouette
    (target Frontier hex) based on which source entry has luminance
    matching the requested layer palette.
    """
    src = im.load()
    w, h = im.size
    # collect the 2 unique colors present in the mode-downscaled image
    palette: dict[tuple[int, int, int], int] = {}
    for y in range(h):
        for x in range(w):
            c = src[x, y][:3]
            palette[c] = palette.get(c, 0) + 1
    if len(palette) < 2:
        raise RuntimeError(
            f"expected 2 colors in downscaled source but found {len(palette)}: {palette}"
        )
    top2 = sorted(palette.items(), key=lambda kv: -kv[1])[:2]
    c0, c1 = top2[0][0], top2[1][0]
    # brighter is silhouette when the layer's silhouette RGB is brighter
    # than its sky RGB (true for both far and mid layers in this project)
    sil_target_brighter = _luminance(silhouette_rgb) >= _luminance(sky_rgb)
    if (_luminance(c0) >= _luminance(c1)) == sil_target_brighter:
        sil_color = c0
    else:
        sil_color = c1

    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    dst = out.load()
    for y in range(h):
        for x in range(w):
            c = src[x, y][:3]
            if c == sil_color:
                dst[x, y] = (silhouette_rgb[0], silhouette_rgb[1], silhouette_rgb[2], 255)
            else:
                dst[x, y] = (0, 0, 0, 0)
    return out


def _height_map(im: Image.Image) -> list[int]:
    """Per column, the y of the topmost opaque pixel (or `h` if fully transparent)."""
    px = im.load()
    w, h = im.size
    heights = [h] * w
    for x in range(w):
        for y in range(h):
            if px[x, y][3] != 0:
                heights[x] = y
                break
    return heights


def _rebuild_from_heights(
    heights: list[int], canvas_size: tuple[int, int], color: tuple[int, int, int]
) -> Image.Image:
    w, h = canvas_size
    out = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    px = out.load()
    for x in range(w):
        top = heights[x]
        for y in range(top, h):
            px[x, y] = (color[0], color[1], color[2], 255)
    return out


def _seam_blend(im: Image.Image, silhouette_rgb: tuple[int, int, int]) -> Image.Image:
    """Cross-blend the height maps at the horizontal wrap seam.

    For each column at distance `i` from the seam (i in 0..n), linearly
    interpolate its height toward the symmetric column on the other
    edge, with weight `1 - i/n`. At i=0 both edges collapse to the
    average height (perfect wrap match); at i=n they revert to the
    original (interior untouched). Applied symmetrically to both
    edges. Silhouettes are ground-anchored (rising from the bottom)
    so blending only the top-of-silhouette height preserves the flat
    2-color pixel-art style — no partial alpha, no soft edges.
    """
    heights = _height_map(im)
    w, _ = im.size
    n = SEAM_BLEND
    if n * 2 >= w:
        return im
    blended = heights[:]
    for i in range(n):
        alpha = 1.0 - (i / n)  # 1 at seam, 0 at far end of blend zone
        left_x = i
        right_x = w - 1 - i
        m = (heights[left_x] + heights[right_x]) / 2.0
        blended[left_x] = int(round(m * alpha + heights[left_x] * (1 - alpha)))
        blended[right_x] = int(round(m * alpha + heights[right_x] * (1 - alpha)))
    return _rebuild_from_heights(blended, im.size, silhouette_rgb)


def _process(source: Path, layer: str) -> Image.Image:
    """Pre-classify at source resolution, then mode-downscale.

    The prior LANCZOS-first approach blended tiny features (telegraph
    poles, distant buildings, pines) with the sky before the palette
    classifier could see them, so their averaged color landed closer to
    `sky_rgb` than `silhouette_rgb` and they got erased. Fix: classify
    at NATIVE resolution first (each source pixel is snapped to sky
    (transparent) or silhouette by RGB distance to the target Frontier
    palette), then majority-color downscale to the 256x144 canvas so
    output cells that had any silhouette majority in their source patch
    stay silhouette.
    """
    sky_rgb, sil_rgb = LAYER_PALETTES[layer]
    src = Image.open(source).convert("RGB")
    sw, sh = src.size
    sp = src.load()

    # 1. Sample the DOMINANT sky color from the top strip of the source
    #    (the AI's actual sky pixel value may differ from the target
    #    palette hex — palette-hex is where we REMAP, but classification
    #    must key off what's actually in the source), then classify each
    #    pixel: any pixel within a small RGB-distance radius of that
    #    sampled sky color becomes sky (transparent); everything else is
    #    silhouette. This handles AIs that render features with a third
    #    tone that isn't the requested silhouette color (e.g. distant
    #    buildings drawn as a darker purple than the sky itself).
    top_hist: dict[tuple[int, int, int], int] = {}
    top_strip = max(4, sh // 12)
    for y in range(0, top_strip):
        for x in range(0, sw, 4):
            c = sp[x, y]
            top_hist[c] = top_hist.get(c, 0) + 1
    sky_actual = max(top_hist.items(), key=lambda kv: kv[1])[0]
    SKY_RADIUS_SQ = 30 * 30
    classified = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    cp = classified.load()
    for y in range(sh):
        for x in range(sw):
            c = sp[x, y]
            d_sky = (
                (c[0] - sky_actual[0]) ** 2
                + (c[1] - sky_actual[1]) ** 2
                + (c[2] - sky_actual[2]) ** 2
            )
            if d_sky > SKY_RADIUS_SQ:
                cp[x, y] = (sil_rgb[0], sil_rgb[1], sil_rgb[2], 255)

    # 2. "Any-silhouette-wins" downscale: an output cell is silhouette if
    #    ANY source pixel in its patch was classified as silhouette. This
    #    preserves narrow features (telegraph poles, distant buildings,
    #    pine tops) that would be killed by a majority-vote mode
    #    downscale. Backgrounds don't need pristine flat regions the way
    #    actors do — a slightly rougher silhouette edge reads fine on a
    #    receding parallax layer and small features surviving is much
    #    more visually important.
    sw, sh = classified.size
    tw, th = CANVAS
    out = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    cp2 = classified.load()
    op = out.load()
    for oy in range(th):
        y0 = oy * sh // th
        y1 = max(y0 + 1, (oy + 1) * sh // th)
        for ox in range(tw):
            x0 = ox * sw // tw
            x1 = max(x0 + 1, (ox + 1) * sw // tw)
            hit = False
            for sy in range(y0, y1):
                if hit:
                    break
                for sx in range(x0, x1):
                    if cp2[sx, sy][3] != 0:
                        hit = True
                        break
            if hit:
                op[ox, oy] = (sil_rgb[0], sil_rgb[1], sil_rgb[2], 255)

    # 3. Erase tiny silhouette speckle from source AA noise.
    out = _erase_islands(out, min_size=3)

    # 4. Seam-repair for horizontal wrap.
    out = _seam_blend(out, sil_rgb)
    return out


def _erase_islands(im: Image.Image, min_size: int) -> Image.Image:
    """Remove opaque components smaller than `min_size`."""
    px = im.load()
    w, h = im.size
    seen = [[False] * w for _ in range(h)]
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
            if len(cells) < min_size:
                for cx, cy in cells:
                    px[cx, cy] = (0, 0, 0, 0)
    return im


def validate(im: Image.Image, layer: str) -> list[str]:
    problems: list[str] = []
    if im.size != CANVAS:
        problems.append(f"canvas is {im.size}, expected {CANVAS}")
    px = im.load()
    _, sil = LAYER_PALETTES[layer]
    transparent = 0
    silhouette = 0
    other = 0
    for y in range(im.size[1]):
        for x in range(im.size[0]):
            c = px[x, y]
            if c[3] == 0:
                transparent += 1
            elif c[:3] == sil:
                silhouette += 1
            else:
                other += 1
    if transparent == 0:
        problems.append("no transparent sky pixels — background will be fully opaque")
    if silhouette == 0:
        problems.append("no silhouette pixels — background will be fully transparent")
    if other > 0:
        problems.append(f"{other} pixels off-palette (expected only silhouette or transparent)")
    # tileability check: left/right column heights must match after seam blend
    lh = _height_map(im)
    if lh[0] != lh[-1]:
        problems.append(f"seam heights differ: left={lh[0]} right={lh[-1]}")
    return problems


def main() -> None:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--layer", choices=tuple(LAYER_PALETTES.keys()), default=None)
    args = parser.parse_args()

    layers = [args.layer] if args.layer else list(LAYER_PALETTES.keys())
    DEST_DIR.mkdir(parents=True, exist_ok=True)
    failures = 0
    for layer in layers:
        source = SOURCE_DIR / f"bg_{layer}.png"
        if not source.exists():
            raise SystemExit(
                f"Missing AI source: {source}. Run generate_background_frames.py first."
            )
        im = _process(source, layer)
        problems = validate(im, layer)
        dest = DEST_DIR / f"{layer}.png"
        im.save(dest)
        status = "OK" if not problems else "WARN"
        print(f"[{status}] {dest.relative_to(REPO_ROOT)}", end="")
        if problems:
            failures += 1
            print(f"  <- {'; '.join(problems)}")
        else:
            print()
    if failures:
        raise SystemExit(f"{failures} layer(s) failed validation")


if __name__ == "__main__":
    main()
