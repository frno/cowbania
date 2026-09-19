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


def player(frame, state):
    im = canvas()
    d = ImageDraw.Draw(im)
    bob = 1 if state == "run" and frame in (1, 4) else 0
    lean = 1 if state in ("dash", "shoot") else 0
    if state == "hurt":
        lean = -1 if frame == 0 else 1
    # Hat, head, poncho, revolver and boots; all frames retain the (8,13) foot anchor.
    rect(d, (4 + lean, 2 + bob, 11 + lean, 3 + bob), C["outline"])
    rect(d, (6 + lean, 1 + bob, 10 + lean, 2 + bob), C["outline"])
    rect(d, (7 + lean, 2 + bob, 10 + lean, 2 + bob), C["ochre"])
    rect(d, (6 + lean, 4 + bob, 10 + lean, 6 + bob), C["outline"])
    rect(d, (7 + lean, 4 + bob, 9 + lean, 5 + bob), C["bone"])
    poly(d, [(5 + lean, 6 + bob), (11 + lean, 6 + bob), (12 + lean, 10), (8, 11), (4 + lean, 10)], C["outline"])
    poly(d, [(6 + lean, 7 + bob), (10 + lean, 7 + bob), (10 + lean, 9), (8, 10), (5 + lean, 9)], C["rust"])
    rect(d, (9 + lean, 7 + bob, 12 + lean, 8 + bob), C["outline"])
    rect(d, (11 + lean, 7 + bob, 13 + lean, 7 + bob), C["bone"])
    if state == "shoot":
        rect(d, (12 + lean, 6 + bob, 14, 7 + bob), C["outline"])
        if frame == 1:
            rect(d, (15, 6 + bob, 15, 6 + bob), C["gold"])
    if state == "reload":
        hand_x = (11, 10, 8, 10)[frame]
        rect(d, (hand_x, 8, hand_x + 1, 9), C["bone"])
    if state == "dash":
        poly(d, [(4, 7), (1, 8 + frame % 2), (4, 9)], C["rust"])
    leg = {
        "run": [(-2, 1), (-1, 2), (0, 1), (2, -1), (1, -2), (0, -1)][frame],
        "jump": [(-1, 1), (1, -1)][frame] if state == "jump" else (0, 0),
        "fall": [(1, -1), (-1, 1)][frame] if state == "fall" else (0, 0),
        "dash": [(-1, 1), (0, 0), (1, -1)][frame] if state == "dash" else (0, 0),
    }.get(state, (0, 0))
    rect(d, (6 + leg[0], 10, 7 + leg[0], 12), C["outline"])
    rect(d, (9 + leg[1], 10, 10 + leg[1], 12), C["outline"])
    rect(d, (5 + leg[0], 12, 8, 13), C["deep"])
    rect(d, (8, 12, 11 + leg[1], 13), C["deep"])
    rect(d, (8, 13, 8, 13), C["sand"])
    if state == "hurt":
        rect(d, (3 if frame == 0 else 13, 5, 3 if frame == 0 else 13, 7), C["red"])
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
    if kind == "Currency":
        poly(d, [(8, y), (12, y + 3), (8, y + 7), (4, y + 3)], C["outline"])
        poly(d, [(8, y + 1), (10, y + 3), (8, y + 6), (6, y + 3)], C["gold"])
        rect(d, (8, y + 2, 8, y + 4), C["white"])
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
    size = (32, 32) if name in ("checkpoint", "shortcut", "transition_gate", "wagon_debris", "mine_timber") else (16, 16)
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
    elif name == "currency":
        poly(d, [(8, 1), (14, 8), (8, 15), (2, 8)], C["outline"])
        poly(d, [(8, 3), (12, 8), (8, 13), (4, 8)], C["gold"])
        rect(d, (7, 5, 9, 10), C["ochre"])
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
    player_counts = {"idle": 4, "run": 6, "jump": 2, "fall": 2, "shoot": 3, "reload": 4, "hurt": 2, "dash": 3}
    for state, count in player_counts.items():
        for frame in range(count):
            save(player(frame, state), f"Player/{state}_{frame}.png")
    enemy_counts = {"patrol": 4, "notice": 2, "attack": 4, "defeated": 2}
    for state, count in enemy_counts.items():
        for frame in range(count):
            save(bandit(frame, state), f"Bandit/{state}_{frame}.png")
            wildlife_state = "lunge" if state == "attack" else state
            save(wildlife(frame, wildlife_state), f"Wildlife/{wildlife_state}_{frame}.png")
    for kind in ("Currency", "Health", "Ammo"):
        for frame in range(4):
            save(pickup(kind, frame), f"Pickup/{kind}/float_{frame}.png")
    for name in ("ground_cap", "ground_body", "platform_left", "platform_middle", "platform_right", "timber_support", "stone", "mine_reinforcement"):
        save(terrain(name), f"Terrain/{name}.png")
    for name in ("checkpoint", "shortcut", "transition_gate", "cactus_0", "cactus_1", "crate", "wagon_debris", "mine_timber", "sign"):
        save(prop(name), f"Props/{name}.png")
    effects = {"muzzle": 3, "impact": 3, "dust": 3, "dash": 3, "hurt": 2, "defeat": 3, "pickup": 4}
    for kind, count in effects.items():
        for frame in range(count):
            save(effect(kind, frame), f"Effects/{kind}_{frame}.png")
    for name in ("heart_full", "heart_empty", "ammo_full", "ammo_empty", "currency", "slot_frame", "panel_corner"):
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
        add(f"Player/{state}", count)
    for state, count in {"patrol":4, "notice":2, "attack":4, "defeated":2}.items():
        add(f"Bandit/{state}", count)
    for state, count in {"patrol":4, "notice":2, "lunge":4, "defeated":2}.items():
        add(f"Wildlife/{state}", count)
    for kind in ("Currency", "Health", "Ammo"):
        add(f"Pickup/{kind}/float", 4)
    for name in ("ground_cap", "ground_body", "platform_left", "platform_middle", "platform_right", "timber_support", "stone", "mine_reinforcement"):
        add(f"Terrain/{name}.png")
    for name in ("checkpoint", "shortcut", "transition_gate", "wagon_debris", "mine_timber"):
        add(f"Props/{name}.png", size=(32, 32))
    for name in ("cactus_0", "cactus_1", "crate", "sign"):
        add(f"Props/{name}.png")
    for kind, count in {"muzzle":3, "impact":3, "dust":3, "dash":3, "hurt":2, "defeat":3, "pickup":4}.items():
        add(f"Effects/{kind}", count)
    for name in ("heart_full", "heart_empty", "ammo_full", "ammo_empty", "currency", "slot_frame", "panel_corner"):
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
            assert im.getpixel((8, 13))[3] == 255, f"{path.name} lost feet anchor"
            assert im.getbbox()[3] <= 14, f"{path.name} extends below feet anchor"
    print(f"Validated {len(expected)} RGBA PNG assets.")


if __name__ == "__main__":
    generate()
    validate()
