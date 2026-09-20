from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]

C = {
    "outline": (35, 24, 32, 255),
    "deep": (57, 35, 38, 255),
    "rust": (142, 66, 42, 255),
    "ochre": (203, 133, 54, 255),
    "sand": (239, 190, 95, 255),
    "timber": (104, 60, 42, 255),
    "timber_hi": (165, 99, 52, 255),
    "bone": (224, 204, 157, 255),
    "violet": (80, 72, 101, 255),
    "blue": (83, 103, 120, 255),
    "blue_dark": (54, 65, 83, 255),
    "sage": (92, 111, 83, 255),
    "red": (190, 61, 48, 255),
    "gold": (241, 179, 54, 255),
    "white": (255, 231, 170, 255),
}


def canvas(size=(16, 16)):
    return Image.new("RGBA", size, (0, 0, 0, 0))


def save(img, rel):
    path = ROOT / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, optimize=False, compress_level=9)


def rect(d, xy, color):
    d.rectangle(xy, fill=color)


def poly(d, points, color):
    d.polygon(points, fill=color)


PLAYER_CANVAS = (32, 32)
PLAYER_FEET = (16, 27)
PLAYER_EFFECT = (25, 15)


def _player_hat(d, cx, cy):
    # Wide-brim cowboy hat. cx,cy = crown top-center. Silhouette must NOT read as a cap:
    # tall crown with a rust band, and a brim wider than the shoulders.
    # Crown (5 wide x 4 tall) with pinched top corners.
    rect(d, (cx - 1, cy, cx + 2, cy), C["outline"])                       # crown top edge
    rect(d, (cx - 2, cy + 1, cx + 3, cy + 1), C["outline"])               # crown shoulders
    rect(d, (cx - 1, cy + 1, cx + 2, cy + 1), C["deep"])                  # crown fill row 1
    rect(d, (cx - 2, cy + 2, cx - 2, cy + 2), C["outline"])
    rect(d, (cx + 3, cy + 2, cx + 3, cy + 2), C["outline"])
    rect(d, (cx - 1, cy + 2, cx + 2, cy + 2), C["rust"])                  # rust band accent
    rect(d, (cx - 2, cy + 3, cx - 2, cy + 3), C["outline"])
    rect(d, (cx + 3, cy + 3, cx + 3, cy + 3), C["outline"])
    rect(d, (cx - 1, cy + 3, cx + 2, cy + 3), C["deep"])                  # crown base
    # Brim (14 wide, wider than shoulders). Dark silhouette with an asymmetric highlight
    # (upper-left lit, right side shadowed) — reads as a proper wide-brim hat, not a cap.
    by = cy + 4
    rect(d, (cx - 5, by, cx + 6, by), C["outline"])                       # brim top edge (dark)
    rect(d, (cx - 6, by, cx - 6, by), C["outline"])                       # curled-up left tip
    rect(d, (cx + 7, by, cx + 7, by), C["outline"])                       # curled-up right tip
    rect(d, (cx - 6, by + 1, cx + 7, by + 1), C["outline"])               # brim widest row edges
    rect(d, (cx - 5, by + 1, cx + 6, by + 1), C["timber"])                # brim body (dark timber)
    rect(d, (cx - 5, by + 1, cx, by + 1), C["timber_hi"])                 # lit left half (upper-left light)
    rect(d, (cx - 5, by + 2, cx + 6, by + 2), C["outline"])               # brim under-shadow
    rect(d, (cx - 6, by + 2, cx - 6, by + 2), C["outline"])
    rect(d, (cx + 7, by + 2, cx + 7, by + 2), C["outline"])
    # Under-brim shadow catches the face top — drawn by face routine.


def _player_face(d, cx, fy, facing=1):
    # Face sits directly under the brim. facing=+1 looks right, -1 looks left.
    # Row fy: brim-shadow eye slit (all outline) with ONE subtle glint on visible eye.
    rect(d, (cx - 3, fy, cx + 2, fy), C["outline"])                       # eye-shadow strip (full width)
    glint_x = cx + 1 if facing >= 0 else cx - 2
    rect(d, (glint_x, fy, glint_x, fy), C["bone"])                        # single warm glint
    # Row fy+1: nose bridge, cheeks (mid-tone skin, no bright sand — keeps face grounded).
    rect(d, (cx - 3, fy + 1, cx + 2, fy + 1), C["outline"])
    rect(d, (cx - 2, fy + 1, cx + 1, fy + 1), C["ochre"])                 # cheeks (mid skin)
    nose_x = cx if facing >= 0 else cx - 1
    rect(d, (nose_x, fy + 1, nose_x, fy + 1), C["sand"])                  # nose highlight (single pixel)
    # Row fy+2: mustache — thick bar, one pixel narrower than face so it reads as a feature not a stripe.
    rect(d, (cx - 3, fy + 2, cx + 2, fy + 2), C["ochre"])                 # skin under mustache
    rect(d, (cx - 2, fy + 2, cx + 1, fy + 2), C["outline"])               # mustache bar (4 wide, inset)
    # Row fy+3: chin with a scar/notch on one cheek for attitude.
    rect(d, (cx - 3, fy + 3, cx + 2, fy + 3), C["outline"])
    rect(d, (cx - 2, fy + 3, cx + 1, fy + 3), C["ochre"])
    scar_x = cx - 2 if facing >= 0 else cx + 1
    rect(d, (scar_x, fy + 3, scar_x, fy + 3), C["deep"])                  # scar pixel
    # Row fy+4: jaw base outline.
    rect(d, (cx - 2, fy + 4, cx + 1, fy + 4), C["outline"])


def _player_kerchief(d, cx, ky):
    # Bandana knotted under chin — extra silhouette below head.
    rect(d, (cx - 4, ky, cx + 3, ky), C["outline"])                       # kerchief top edge
    rect(d, (cx - 4, ky + 1, cx + 3, ky + 1), C["red"])                   # kerchief body
    rect(d, (cx - 5, ky + 1, cx - 5, ky + 1), C["outline"])               # left tip
    rect(d, (cx + 4, ky + 1, cx + 4, ky + 1), C["outline"])               # right tip
    rect(d, (cx - 1, ky + 1, cx, ky + 1), C["outline"])                   # knot shadow


def _player_poncho(d, cx, py, facing=1):
    # Poncho: 12 wide at shoulders, tapered fringe hem. Bold shape reads at any zoom.
    # Shoulder outline row.
    rect(d, (cx - 6, py, cx + 5, py), C["outline"])
    # Body rows py+1..py+4.
    for r in range(1, 5):
        y = py + r
        rect(d, (cx - 6, y, cx + 5, y), C["outline"])
        rect(d, (cx - 5, y, cx + 4, y), C["rust"])
    # Shadowed side (poncho hangs, light from upper-left => shadow on right).
    shadow_x0, shadow_x1 = (cx + 2, cx + 4) if facing >= 0 else (cx - 5, cx - 3)
    rect(d, (shadow_x0, py + 1, shadow_x1, py + 4), C["deep"])
    # Highlight column on lit side.
    hi_x = cx - 4 if facing >= 0 else cx + 3
    rect(d, (hi_x, py + 2, hi_x, py + 3), C["ochre"])
    # Woven horizontal stripe (bone) crossing the chest.
    rect(d, (cx - 4, py + 2, cx + 1, py + 2), C["bone"])
    if facing >= 0:
        rect(d, (cx + 3, py + 2, cx + 3, py + 2), C["deep"])              # keep shadow reading
    # Hem outline + fringe teeth.
    rect(d, (cx - 6, py + 5, cx + 5, py + 5), C["outline"])
    for x in (cx - 5, cx - 3, cx - 1, cx + 1, cx + 3):
        rect(d, (x, py + 6, x, py + 6), C["deep"])                        # fringe teeth
    for x in (cx - 4, cx - 2, cx, cx + 2, cx + 4):
        rect(d, (x, py + 6, x, py + 6), C["rust"])


def _player_belt(d, cx, y):
    rect(d, (cx - 4, y, cx + 3, y), C["timber"])
    rect(d, (cx - 4, y, cx - 4, y), C["outline"])
    rect(d, (cx + 3, y, cx + 3, y), C["outline"])
    rect(d, (cx - 1, y, cx, y), C["gold"])                                # buckle


def _player_legs(d, cx, top_y, foot_y, left_dx, right_dx, facing=1):
    # Two independent legs. dx offsets the foot horizontally to make stride poses.
    # Each leg: pants column (2 wide x 2 tall) + boot wedge (2 wide x 1 tall) at foot_y.
    # Left leg (screen-left), right leg (screen-right).
    for side, dx in ((-1, left_dx), (+1, right_dx)):
        base_x = cx - 3 if side < 0 else cx + 2
        # Pants (2 rows).
        rect(d, (base_x, top_y, base_x + 1, top_y + 1), C["blue_dark"])
        rect(d, (base_x + (0 if side < 0 else 1), top_y, base_x + (0 if side < 0 else 1), top_y + 1), C["blue"])
        # Knee shadow.
        rect(d, (base_x, top_y + 1, base_x, top_y + 1), C["outline"]) if side < 0 else rect(d, (base_x + 1, top_y + 1, base_x + 1, top_y + 1), C["outline"])
        # Boot (row top_y+2, shifted by dx).
        bx = base_x + dx
        rect(d, (bx - 1, foot_y - 1, bx + 2, foot_y - 1), C["deep"])      # boot shaft
        rect(d, (bx - 1, foot_y - 1, bx - 1, foot_y - 1), C["outline"])   # heel edge
        rect(d, (bx + 2, foot_y - 1, bx + 2, foot_y - 1), C["outline"])   # toe edge
        rect(d, (bx, foot_y - 1, bx + 1, foot_y - 1), C["timber_hi"])     # boot fold highlight
        # Sole row (foot_y — the anchor row).
        rect(d, (bx - 1, foot_y, bx + 2, foot_y), C["outline"])
        # Tiny gold spur behind heel (rear side of each boot).
        spur_x = bx - 2 if side < 0 else bx + 3
        if 0 <= spur_x < 32:
            rect(d, (spur_x, foot_y - 1, spur_x, foot_y - 1), C["gold"])


def _player_revolver(d, tip_x, tip_y, dir_x, dir_y=0, style="hold"):
    # dir_x = +1 aim right, -1 aim left, 0 hip-holstered. dir_y in {-1,0,+1} for angle.
    # style: "hold" (idle/at hip), "aim" (arm extended forward), "up" (arm raised for reload).
    if style == "hip":
        # Grip peeks out below the poncho hem on the right side.
        rect(d, (tip_x, tip_y, tip_x + 1, tip_y + 1), C["blue_dark"])      # grip
        rect(d, (tip_x + 2, tip_y, tip_x + 2, tip_y), C["gold"])           # hammer glint
        return
    if style == "aim":
        # Arm extended: forearm 3 pixels, cylinder 2, barrel 2.
        d_ = dir_x
        # Forearm (skin).
        rect(d, (tip_x - 4 * d_, tip_y, tip_x - 2 * d_, tip_y), C["ochre"])
        rect(d, (tip_x - 4 * d_, tip_y - 1, tip_x - 2 * d_, tip_y - 1), C["outline"])
        # Cylinder.
        rect(d, (tip_x - 1 * d_, tip_y, tip_x - 1 * d_, tip_y), C["blue_dark"])
        rect(d, (tip_x - 1 * d_, tip_y - 1, tip_x - 1 * d_, tip_y - 1), C["outline"])
        rect(d, (tip_x - 1 * d_, tip_y + 1, tip_x - 1 * d_, tip_y + 1), C["outline"])
        # Barrel (steel).
        rect(d, (tip_x, tip_y, tip_x + d_, tip_y), C["blue"])
        rect(d, (tip_x, tip_y - 1, tip_x + d_, tip_y - 1), C["outline"])
        rect(d, (tip_x, tip_y + 1, tip_x + d_, tip_y + 1), C["outline"])
        rect(d, (tip_x + d_, tip_y, tip_x + d_, tip_y), C["bone"])         # muzzle tip glint
        return
    if style == "up":
        # Reload pose: gun cocked upward at chest, cylinder open.
        rect(d, (tip_x, tip_y - 3, tip_x + 1, tip_y - 3), C["blue"])       # barrel up
        rect(d, (tip_x, tip_y - 4, tip_x + 1, tip_y - 4), C["outline"])
        rect(d, (tip_x - 1, tip_y - 2, tip_x + 2, tip_y - 1), C["blue_dark"])  # cylinder+frame
        rect(d, (tip_x - 1, tip_y - 2, tip_x - 1, tip_y - 2), C["outline"])
        rect(d, (tip_x + 2, tip_y - 2, tip_x + 2, tip_y - 1), C["outline"])
        rect(d, (tip_x, tip_y, tip_x + 1, tip_y + 1), C["deep"])           # grip
        return


def player(frame, state):
    """DEPRECATED — no longer authoritative.

    Player art is now produced by the AI + pixelate pipeline in
    ``tools/nanogpt/pixelate_sprite.py``, using AI-generated pose
    references in ``tools/nanogpt/out/hero_*.png`` (see the "Player
    art pipeline" section in ``Assets/Art/Frontier/manifest.md``).

    This procedural implementation is retained only so ad-hoc PIL
    experiments still import cleanly. The top-level ``generate()``
    entry point below no longer calls it, and ``validate()`` still
    checks the on-disk files produced by the pipeline. If you need to
    regenerate player PNGs, run::

        python tools/nanogpt/pixelate_sprite.py

    Do NOT re-enable this function's call site: the previous
    procedural output was rejected as unreadable at 32x32 during the
    Release 6 art refresh.
    """
    im = Image.new("RGBA", PLAYER_CANVAS, (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = 16          # centerline column
    foot_y = 27      # feet anchor row
    facing = 1       # frames are authored right-facing; renderer flips for facing left.

    # -------- Per-state pose parameters --------
    bob = 0
    lean = 0
    if state == "run":
        # 6-frame stride cycle: subtle vertical bob + slight forward lean on push-off.
        bob = (0, -1, 0, 0, -1, 0)[frame]
        lean = (1, 1, 0, 1, 1, 0)[frame]
    elif state == "jump":
        bob = -1
        lean = 1
    elif state == "fall":
        bob = 0
        lean = -1 if frame == 0 else 0
    elif state == "shoot":
        lean = 1
        bob = 0 if frame != 2 else -1                                     # recoil kick on frame 2
    elif state == "reload":
        bob = 0
        lean = 0
    elif state == "hurt":
        lean = -3 if frame == 0 else 2                                    # sharp recoil then whip
        bob = 1 if frame == 0 else 0
    elif state == "dash":
        lean = 2 + (frame % 2)
        bob = 0 if frame == 1 else -1

    # -------- Body (hat + face + kerchief + poncho + belt) --------
    crown_top = 2 + bob                                                   # y where crown top sits
    _player_hat(d, cx + lean, crown_top)
    face_top = crown_top + 7                                              # eye row
    _player_face(d, cx + lean, face_top, facing=1 if state != "hurt" or frame != 0 else -1)
    ky = face_top + 5                                                     # kerchief top row
    _player_kerchief(d, cx + (lean // 2), ky)
    py = ky + 2                                                           # poncho shoulders row
    _player_poncho(d, cx, py, facing=1)
    belt_y = py + 7                                                       # belt row
    _player_belt(d, cx, belt_y)

    # -------- Legs (stride patterns) --------
    top_y = belt_y + 1                                                    # pants top row
    if state == "run":
        # Big alternating stride: cycle through 6 leg configurations.
        strides = [(-2, +2), (-1, +1), (+1, -1), (+2, -2), (+1, -1), (-1, +1)]
        ldx, rdx = strides[frame]
    elif state == "idle":
        # Subtle idle sway.
        drift = 0 if frame in (0, 2) else (-1 if frame == 1 else 1)
        ldx, rdx = (drift, -drift)
    elif state == "jump":
        # Legs tucked slightly under (forward foot).
        ldx, rdx = ((+1, +1), (+2, 0))[frame]
    elif state == "fall":
        # Legs spread for landing prep.
        ldx, rdx = ((-1, +1), (-2, +2))[frame]
    elif state == "shoot":
        # Firm braced stance, back leg planted.
        ldx, rdx = (-1, +1)
    elif state == "reload":
        ldx, rdx = (0, 0)
    elif state == "hurt":
        # Knocked back — feet slide backward (left).
        ldx, rdx = (-2, -2) if frame == 0 else (-1, +1)
    elif state == "dash":
        # Streaking forward — back leg trailing, front leg extended.
        ldx, rdx = [(-2, +3), (-3, +2), (-1, +3)][frame]
    else:
        ldx, rdx = (0, 0)

    _player_legs(d, cx, top_y, foot_y, ldx, rdx, facing=1)

    # -------- Right arm / revolver --------
    hip_x = cx + 4                                                        # right hip
    hip_y = belt_y - 1
    if state == "shoot":
        # Extended forward, muzzle at (~25, ~15).
        tip_x = 25
        tip_y = 15 + bob
        _player_revolver(d, tip_x, tip_y, dir_x=+1, style="aim")
        if frame == 1:
            # Muzzle flash burst (kept small; the Effects/muzzle_*.png handles the big flash).
            rect(d, (tip_x + 1, tip_y - 1, tip_x + 2, tip_y + 1), C["gold"])
            rect(d, (tip_x + 2, tip_y, tip_x + 2, tip_y), C["sand"])
        if frame == 2:
            # Recoil: muzzle rises 1 px, hand pulls back 1 px.
            _player_revolver(d, tip_x - 1, tip_y - 1, dir_x=+1, style="aim")
    elif state == "reload":
        # Gun raised to chest, off-hand feeding cartridges.
        _player_revolver(d, cx + 4, belt_y - 2, dir_x=+1, style="up")
        # Off-hand (left) cycles between belt and cylinder.
        hand_pos = [(cx - 2, belt_y + 1), (cx, py + 5), (cx + 2, py + 4), (cx, py + 5)][frame]
        rect(d, hand_pos + (hand_pos[0] + 1, hand_pos[1]), C["ochre"])
        rect(d, (hand_pos[0], hand_pos[1] - 1, hand_pos[0] + 1, hand_pos[1] - 1), C["outline"])
        if frame in (1, 2):
            # Cartridge in hand.
            rect(d, (hand_pos[0], hand_pos[1] - 2, hand_pos[0], hand_pos[1] - 2), C["gold"])
    elif state == "hurt":
        # Gun flails: barrel points down.
        gx = cx + 4 if frame == 0 else cx + 3
        rect(d, (gx, belt_y, gx + 1, belt_y + 2), C["blue_dark"])
        rect(d, (gx, belt_y, gx, belt_y), C["outline"])
        rect(d, (gx + 1, belt_y + 2, gx + 1, belt_y + 2), C["outline"])
    elif state == "dash":
        # Gun clutched close to body.
        _player_revolver(d, hip_x, hip_y, dir_x=+1, style="hip")
    elif state in ("jump", "fall"):
        # Gun raised in one hand for silhouette.
        _player_revolver(d, cx + 5, py + 2, dir_x=+1, style="aim")
    else:
        # Idle / run: holstered peek at right hip.
        _player_revolver(d, hip_x, hip_y, dir_x=+1, style="hip")

    # -------- State-specific accents --------
    if state == "dash":
        # Speed streaks behind (upper-left) — reinforces motion direction.
        for i, y in enumerate((py + 1, py + 3, belt_y + 1)):
            rect(d, (1 + frame, y, 5 + frame + i, y), C["blue"])
            rect(d, (2 + frame, y + 1, 4 + frame + i, y + 1), C["violet"])
    if state == "hurt":
        # Damage burst near torso.
        bx = 5 if frame == 0 else 26
        rect(d, (bx, py + 2, bx + 1, py + 3), C["red"])
        rect(d, (bx - 1, py + 3, bx, py + 4), C["red"])
    if state == "jump":
        # Coat/poncho lift: small trailing pixels below hem.
        rect(d, (cx - 4, py + 7, cx - 3, py + 7), C["deep"])
        rect(d, (cx + 3, py + 7, cx + 4, py + 7), C["deep"])
    if state == "fall":
        # Hair/hat brim flutter — extra dark pixel behind brim.
        rect(d, (cx - 8, 6, cx - 7, 6), C["outline"])

    # -------- Anchor guarantee --------
    # Feet anchor at (16, 27) must be opaque (validator asserts it).
    if im.getpixel((16, 27))[3] == 0:
        rect(d, (16, 27, 16, 27), C["outline"])

    return im


def bandit(frame, state):
    im = canvas()
    d = ImageDraw.Draw(im)
    lean = 1 if state == "attack" and frame > 0 else 0
    rect(d, (4 + lean, 2, 12 + lean, 3), C["outline"])
    rect(d, (6 + lean, 1, 10 + lean, 2), C["outline"])
    rect(d, (7 + lean, 2, 10 + lean, 2), C["blue"])
    rect(d, (6 + lean, 4, 10 + lean, 6), C["outline"])
    rect(d, (7 + lean, 4, 9 + lean, 5), C["bone"])
    rect(d, (5 + lean, 6, 11 + lean, 10), C["outline"])
    rect(d, (6 + lean, 7, 10 + lean, 9), C["violet"])
    gun_y = 7 if state == "attack" else 8
    rect(d, (10 + lean, gun_y, 14, gun_y + 1), C["outline"])
    if state == "notice":
        rect(d, (8, 0, 8, 0), C["gold"])
        if frame == 1:
            rect(d, (10, 0, 10, 0), C["gold"])
    if state == "attack" and frame == 2:
        rect(d, (15, gun_y, 15, gun_y), C["gold"])
    if state == "defeated":
        d = ImageDraw.Draw(im := canvas())
        rect(d, (3, 11 + frame, 12, 13), C["outline"])
        rect(d, (5, 10 + frame, 9, 11 + frame), C["violet"])
        rect(d, (1, 12, 5, 13), C["outline"])
        return im
    step = (-1, 0, 1, 0)[frame] if state == "patrol" else 0
    rect(d, (5 + step, 10, 7 + step, 13), C["deep"])
    rect(d, (9 - step, 10, 11 - step, 13), C["deep"])
    return im


def wildlife(frame, state):
    im = canvas()
    d = ImageDraw.Draw(im)
    if state == "defeated":
        rect(d, (2, 11 + frame, 13, 13), C["outline"])
        rect(d, (4, 10 + frame, 11, 11 + frame), C["timber"])
        return im
    stretch = frame if state == "lunge" else 0
    y = 8 + (frame % 2 if state == "patrol" else 0)
    poly(d, [(2, y), (5, y - 3), (11 + min(stretch, 2), y - 2), (14, y), (12, y + 3), (4, y + 3)], C["outline"])
    poly(d, [(4, y), (6, y - 2), (11, y - 1), (12, y + 1), (5, y + 2)], C["timber_hi"])
    poly(d, [(11, y - 2), (15, y - 1), (13, y)], C["outline"])
    rect(d, (11, y - 1, 11, y - 1), C["gold"])
    rect(d, (3, y + 3, 5, y + 4), C["deep"])
    rect(d, (10, y + 3, 12, y + 4), C["deep"])
    if state == "notice":
        rect(d, (7, 2 - frame, 7, 3), C["gold"])
    if state == "lunge" and frame >= 2:
        rect(d, (0, y + 1, 2, y + 1), C["ochre"])
    return im


def pickup(kind, frame):
    im = canvas()
    d = ImageDraw.Draw(im)
    y = 4 + (0, 1, 2, 1)[frame]
    if kind == "Coin":
        width = (4, 3, 2, 3)[frame]
        rect(d, (8 - width, y, 8 + width, y + 8), C["outline"])
        rect(d, (8 - width + 1, y + 1, 8 + width - 1, y + 7), C["gold"])
        rect(d, (8 - max(0, width - 2), y + 3, 8 + max(0, width - 2), y + 5), C["ochre"])
        rect(d, (7, y + 2, 8, y + 3), C["white"])
    elif kind == "Health":
        poly(d, [(3, y + 2), (5, y), (8, y + 2), (11, y), (13, y + 2), (12, y + 5), (8, y + 9), (4, y + 5)], C["outline"])
        poly(d, [(5, y + 2), (8, y + 4), (11, y + 2), (11, y + 4), (8, y + 7), (5, y + 4)], C["red"])
        rect(d, (6, y + 2, 7, y + 2), C["white"])
    else:
        rect(d, (4, y + 1, 11, y + 7), C["outline"])
        rect(d, (5, y + 2, 10, y + 6), C["ochre"])
        rect(d, (6, y, 7, y + 2), C["bone"])
        rect(d, (9, y, 10, y + 2), C["bone"])
        rect(d, (6, y + 4, 9, y + 5), C["deep"])
    return im


def terrain(name):
    im = canvas()
    d = ImageDraw.Draw(im)
    if name in ("ground_cap", "platform_left", "platform_middle", "platform_right"):
        rect(d, (0, 3, 15, 5), C["outline"])
        rect(d, (0, 3, 15, 3), C["sand"])
        rect(d, (0, 4, 15, 4), C["ochre"])
        rect(d, (0, 5, 15, 15), C["timber"])
        for x in range(2, 16, 5):
            rect(d, (x, 7, x + 1, 14), C["deep"])
        if name == "platform_left":
            rect(d, (0, 3, 1, 15), C["outline"])
        if name == "platform_right":
            rect(d, (14, 3, 15, 15), C["outline"])
    elif name == "ground_body":
        rect(d, (0, 0, 15, 15), C["timber"])
        for y in (3, 9, 14):
            rect(d, (0, y, 15, y), C["deep"])
        rect(d, (2, 1, 4, 2), C["timber_hi"])
        rect(d, (10, 6, 13, 7), C["ochre"])
    elif name == "timber_support":
        rect(d, (5, 0, 10, 15), C["outline"])
        rect(d, (6, 0, 9, 15), C["timber_hi"])
        rect(d, (6, 4, 9, 5), C["deep"])
        rect(d, (6, 11, 9, 12), C["deep"])
    elif name == "stone":
        rect(d, (0, 0, 15, 15), C["outline"])
        rect(d, (1, 1, 14, 14), C["blue_dark"])
        for xy in ((2, 2, 7, 5), (9, 2, 13, 6), (2, 8, 6, 13), (8, 8, 13, 12)):
            rect(d, xy, C["blue"])
    else:
        rect(d, (1, 1, 14, 4), C["outline"])
        rect(d, (2, 2, 13, 3), C["timber_hi"])
        rect(d, (3, 4, 5, 15), C["outline"])
        rect(d, (10, 4, 12, 15), C["outline"])
        rect(d, (4, 5, 11, 6), C["timber"])
    return im


def prop(name):
    size = (32, 32) if name in ("checkpoint", "shortcut", "transition_gate", "trail_bell", "wagon_debris", "mine_timber") else (16, 16)
    im = canvas(size)
    d = ImageDraw.Draw(im)
    s = size[0] // 16
    def R(xy, col):
        rect(d, tuple(v * s for v in xy), col)
    if name == "checkpoint":
        R((6, 2, 9, 14), C["outline"]); R((7, 3, 8, 13), C["timber_hi"])
        poly(d, [(9*s, 3*s), (14*s, 5*s), (9*s, 8*s)], C["outline"])
        poly(d, [(10*s, 4*s), (13*s, 5*s), (10*s, 6*s)], C["ochre"])
    elif name == "shortcut":
        R((3, 3, 12, 14), C["outline"]); R((5, 5, 10, 14), C["blue_dark"])
        R((7, 9, 9, 14), C["deep"]); R((6, 2, 9, 4), C["violet"])
    elif name == "transition_gate":
        R((2, 1, 13, 3), C["outline"]); R((2, 3, 4, 15), C["outline"]); R((11, 3, 13, 15), C["outline"])
        R((3, 2, 12, 2), C["timber_hi"]); R((5, 5, 10, 6), C["violet"])
    elif name == "trail_bell":
        # Tall frontier finish marker: timber post, brass bell, and a long pull rope.
        R((2, 1, 4, 15), C["outline"]); R((3, 2, 3, 14), C["timber_hi"])
        R((3, 1, 12, 3), C["outline"]); R((4, 2, 11, 2), C["timber_hi"])
        R((10, 3, 11, 5), C["outline"])
        poly(d, [(8*s, 5*s), (13*s, 5*s), (14*s, 9*s), (7*s, 9*s)], C["outline"])
        poly(d, [(9*s, 6*s), (12*s, 6*s), (13*s, 8*s), (8*s, 8*s)], C["gold"])
        R((9, 9, 12, 10), C["outline"]); R((10, 9, 11, 9), C["sand"])
        R((12, 10, 12, 15), C["bone"]); R((11, 14, 13, 15), C["outline"])
    elif name.startswith("cactus"):
        R((7, 2, 9, 14), C["outline"]); R((8, 3, 8, 13), C["sage"])
        side = 3 if name.endswith("0") else 11
        R((side, 6, side + 4 if side == 3 else side, 8), C["outline"])
        R((side, 4, side + 1, 7), C["outline"])
    elif name == "crate":
        # Sunken, damaged storage debris: broken crown and muted values prevent platform reading.
        poly(d, [(2, 6), (4, 5), (6, 6), (8, 4), (10, 6), (13, 5), (13, 14), (2, 14)], C["deep"])
        poly(d, [(3, 7), (5, 6), (7, 7), (9, 5), (11, 7), (12, 6), (12, 13), (3, 13)], C["timber"])
        R((4, 8, 5, 12), C["blue_dark"]); R((10, 8, 11, 12), C["blue_dark"])
        R((3, 12, 12, 13), C["deep"])
        R((6, 8, 8, 9), C["timber_hi"]); R((8, 10, 10, 11), C["timber_hi"])
    elif name == "wagon_debris":
        poly(d, [(1*s, 12*s), (5*s, 10*s), (8*s, 11*s), (12*s, 8*s), (15*s, 10*s), (14*s, 13*s), (3*s, 14*s)], C["deep"])
        poly(d, [(5*s, 9*s), (7*s, 6*s), (10*s, 8*s), (13*s, 7*s), (12*s, 11*s), (7*s, 12*s)], C["timber"])
        R((2, 13, 15, 14), C["blue_dark"])
        for cx, cy in ((5, 12), (12, 11)):
            d.ellipse((cx*s-2*s, cy*s-2*s, cx*s+2*s, cy*s+2*s), fill=C["deep"])
            d.rectangle((cx*s-1*s, cy*s, cx*s+1*s, cy*s), fill=C["violet"])
    elif name == "mine_timber":
        poly(d, [(2*s, 5*s), (5*s, 3*s), (8*s, 5*s), (11*s, 2*s), (14*s, 4*s), (13*s, 6*s),
                 (10*s, 5*s), (8*s, 7*s), (5*s, 5*s), (3*s, 7*s)], C["deep"])
        poly(d, [(4*s, 6*s), (6*s, 6*s), (5*s, 15*s), (3*s, 15*s)], C["timber"])
        poly(d, [(11*s, 5*s), (13*s, 6*s), (12*s, 15*s), (10*s, 15*s)], C["timber"])
        poly(d, [(6*s, 8*s), (7*s, 7*s), (11*s, 14*s), (9*s, 14*s)], C["blue_dark"])
    elif name == "sign":
        poly(d, [(7, 8), (9, 8), (10, 15), (7, 15)], C["deep"])
        poly(d, [(2, 5), (4, 3), (9, 4), (11, 3), (14, 5), (12, 9), (7, 8), (4, 10), (2, 8)], C["deep"])
        poly(d, [(3, 5), (5, 4), (9, 5), (11, 4), (12, 5), (11, 8), (7, 7), (4, 9), (3, 8)], C["timber"])
        R((5, 5, 6, 5), C["violet"]); R((8, 6, 10, 6), C["blue_dark"])
    return im


def effect(kind, frame):
    im = canvas()
    d = ImageDraw.Draw(im)
    if kind == "muzzle":
        r = frame + 2
        poly(d, [(8, 8-r), (9, 6), (8+r, 8), (10, 9), (8, 8+r), (7, 10), (8-r, 8), (6, 7)], C["gold"])
        rect(d, (8, 7, 9, 8), C["white"])
    elif kind == "impact":
        for i in range(frame + 2):
            rect(d, (8 - i*2, 8 - i, 8 - i*2, 8 - i), C["sand"])
            rect(d, (8 + i*2, 8 + i, 8 + i*2, 8 + i), C["white"])
    elif kind == "dust":
        boxes = [(5, 9, 10, 12), (3, 7, 7, 10), (9, 6, 13, 9)]
        for i, b in enumerate(boxes[:frame + 1]):
            d.ellipse(b, fill=C["ochre"] if i else C["sand"])
    elif kind == "dash":
        for y in range(5, 12, 3):
            rect(d, (1 + frame, y, 12 - frame, y), C["blue"])
            rect(d, (3 + frame, y + 1, 9, y + 1), C["violet"])
    elif kind == "hurt":
        poly(d, [(8, 1+frame), (9, 6), (14, 4), (11, 8), (15, 11), (9, 10), (8, 15-frame), (7, 10), (1, 12), (5, 8), (2, 4), (7, 6)], C["red"])
    elif kind == "defeat":
        for i in range(3 + frame * 2):
            x = (2 + i * 3) % 14
            y = 4 + ((i * 5 + frame) % 9)
            rect(d, (x, y, x + (frame == 0), y + (frame == 2)), C["violet"] if i % 2 else C["rust"])
    else:
        radius = (2, 4, 6, 3)[frame]
        d.rectangle((8-radius, 8-radius, 8+radius, 8+radius), outline=C["gold"])
        rect(d, (8, 8, 8, 8), C["white"])
    return im


def ui(name):
    im = canvas()
    d = ImageDraw.Draw(im)
    if name.startswith("heart"):
        col = C["red"] if name.endswith("full") else C["blue_dark"]
        poly(d, [(2, 5), (4, 2), (8, 4), (12, 2), (14, 5), (13, 9), (8, 14), (3, 9)], C["outline"])
        poly(d, [(4, 5), (5, 4), (8, 6), (11, 4), (12, 5), (11, 8), (8, 11), (5, 8)], col)
    elif name.startswith("ammo"):
        col = C["ochre"] if name.endswith("full") else C["blue_dark"]
        rect(d, (5, 2, 10, 14), C["outline"]); rect(d, (6, 4, 9, 12), col)
        poly(d, [(6, 4), (8, 1), (9, 4)], C["bone"] if name.endswith("full") else C["violet"])
    elif name == "coin":
        poly(d, [(5, 1), (11, 1), (14, 4), (14, 11), (11, 14), (5, 14), (2, 11), (2, 4)], C["outline"])
        poly(d, [(6, 3), (10, 3), (12, 5), (12, 10), (10, 12), (6, 12), (4, 10), (4, 5)], C["gold"])
        rect(d, (6, 5, 10, 10), C["ochre"])
        rect(d, (6, 4, 8, 5), C["white"])
    elif name == "slot_frame":
        d.rectangle((1, 1, 14, 14), outline=C["outline"], width=2)
        rect(d, (3, 3, 12, 3), C["sand"]); rect(d, (3, 12, 12, 12), C["timber"])
    elif name == "panel_corner":
        rect(d, (1, 1, 14, 3), C["outline"]); rect(d, (1, 1, 3, 14), C["outline"])
        rect(d, (3, 3, 12, 3), C["ochre"]); rect(d, (3, 3, 3, 12), C["ochre"])
    return im


def background(layer):
    im = canvas((256, 144))
    d = ImageDraw.Draw(im)
    far = layer.endswith("far")
    branch = layer.startswith("branch")
    base = C["blue_dark"] if far else C["violet"]
    accent = C["blue"] if far else (100, 76, 91, 255)
    horizon = 78 if far else 96
    if branch:
        poly(d, [(0, horizon), (35, 55), (61, 71), (92, 42), (127, 73), (163, 48), (206, 68), (256, 39), (256, 144), (0, 144)], base)
        for x in range(18, 256, 47):
            poly(d, [(x, horizon+15), (x+7, horizon-22), (x+11, horizon+15)], accent)
    else:
        poly(d, [(0, horizon), (34, 62), (74, 72), (111, 48), (155, 75), (199, 55), (256, 70), (256, 144), (0, 144)], base)
        if not far:
            for x in (24, 82, 174, 225):
                rect(d, (x, 73, x+5, 127), accent)
                rect(d, (x-8, 74, x+13, 80), accent)
    # Irregular, broken silhouettes deliberately avoid continuous platform-like top rims.
    for x in range(0, 256, 32):
        rect(d, (x + 7, 125 + (x//32)%4, x + 17, 143), accent)
    return im


def generate():
    # Player art is produced by tools/nanogpt/pixelate_sprite.py (AI +
    # pixelate pipeline). The procedural `player()` above is retained
    # for reference only — its output was rejected at 32x32 during the
    # Release 6 refresh. Do NOT re-enable it here. Re-render player
    # frames with:  python tools/nanogpt/pixelate_sprite.py
    enemy_counts = {"patrol": 4, "notice": 2, "attack": 4, "defeated": 2}
    for state, count in enemy_counts.items():
        for frame in range(count):
            save(bandit(frame, state), f"Bandit/{state}_{frame}.png")
            wildlife_state = "lunge" if state == "attack" else state
            save(wildlife(frame, wildlife_state), f"Wildlife/{wildlife_state}_{frame}.png")
    for kind in ("Coin", "Health", "Ammo"):
        for frame in range(4):
            save(pickup(kind, frame), f"Pickup/{kind}/float_{frame}.png")
    for name in ("ground_cap", "ground_body", "platform_left", "platform_middle", "platform_right", "timber_support", "stone", "mine_reinforcement"):
        save(terrain(name), f"Terrain/{name}.png")
    for name in ("checkpoint", "shortcut", "transition_gate", "trail_bell", "cactus_0", "cactus_1", "crate", "wagon_debris", "mine_timber", "sign"):
        save(prop(name), f"Props/{name}.png")
    effects = {"muzzle": 3, "impact": 3, "dust": 3, "dash": 3, "hurt": 2, "defeat": 3, "pickup": 4}
    for kind, count in effects.items():
        for frame in range(count):
            save(effect(kind, frame), f"Effects/{kind}_{frame}.png")
    for name in ("heart_full", "heart_empty", "ammo_full", "ammo_empty", "coin", "slot_frame", "panel_corner"):
        save(ui(name), f"UI/{name}.png")
    for name in ("hub_far", "hub_mid", "branch_far", "branch_mid"):
        save(background(name), f"Background/{name}.png")


def validate():
    expected = {}
    def add(rel, count=1, size=(16, 16)):
        if count == 1:
            expected[rel] = size
        else:
            for i in range(count):
                expected[f"{rel}_{i}.png"] = size
    for state, count in {"idle":4, "run":6, "jump":2, "fall":2, "shoot":3, "reload":4, "hurt":2, "dash":3}.items():
        add(f"Player/{state}", count, size=(32, 32))
    for state, count in {"patrol":4, "notice":2, "attack":4, "defeated":2}.items():
        add(f"Bandit/{state}", count)
    for state, count in {"patrol":4, "notice":2, "lunge":4, "defeated":2}.items():
        add(f"Wildlife/{state}", count)
    for state, count in {"patrol":4, "notice":2, "roll":4}.items():
        add(f"Armadillo/{state}", count)
    for state, count in {"hidden":2, "rise":2, "exposed":4, "retreat":2, "defeated":2}.items():
        add(f"Snake/{state}", count)
    for kind in ("Coin", "Health", "Ammo"):
        add(f"Pickup/{kind}/float", 4)
    for name in ("ground_cap", "ground_body", "platform_left", "platform_middle", "platform_right", "timber_support", "stone", "mine_reinforcement"):
        add(f"Terrain/{name}.png")
    for name in ("checkpoint", "shortcut", "transition_gate", "trail_bell", "wagon_debris", "mine_timber"):
        add(f"Props/{name}.png", size=(32, 32))
    for name in ("cactus_0", "cactus_1", "crate", "sign"):
        add(f"Props/{name}.png")
    for kind, count in {"muzzle":3, "impact":3, "dust":3, "dash":3, "hurt":2, "defeat":3, "pickup":4}.items():
        add(f"Effects/{kind}", count)
    for name in ("heart_full", "heart_empty", "ammo_full", "ammo_empty", "coin", "slot_frame", "panel_corner"):
        add(f"UI/{name}.png")
    for name in ("hub_far", "hub_mid", "branch_far", "branch_mid"):
        add(f"Background/{name}.png", size=(256, 144))
    pngs = {p.relative_to(ROOT).as_posix() for p in ROOT.rglob("*.png")}
    assert pngs == set(expected), f"Asset contract mismatch: missing={set(expected)-pngs}, extra={pngs-set(expected)}"
    solid_tiles = {
        "Terrain/ground_cap.png", "Terrain/ground_body.png", "Terrain/platform_left.png",
        "Terrain/platform_middle.png", "Terrain/platform_right.png", "Terrain/stone.png",
    }
    for rel, size in expected.items():
        with Image.open(ROOT / rel) as im:
            assert im.format == "PNG" and im.mode == "RGBA" and im.size == size, (rel, im.format, im.mode, im.size)
            alpha = im.getchannel("A")
            lo, hi = alpha.getextrema()
            assert hi == 255, f"{rel} must contain opaque art pixels"
            if rel not in solid_tiles:
                assert lo == 0, f"{rel} must retain a transparent background"
    for path in (ROOT / "Player").glob("*.png"):
        with Image.open(path) as im:
            assert im.getpixel((16, 27))[3] == 255, f"{path.name} lost feet anchor"
            assert im.getbbox()[3] <= 28, f"{path.name} extends below feet anchor"
    print(f"Validated {len(expected)} RGBA PNG assets.")


if __name__ == "__main__":
    generate()
    validate()
