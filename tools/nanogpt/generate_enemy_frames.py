"""Generate the base-pose PNGs for Cowbania's 16x16 enemy sprites.

Produces one AI image per (enemy, state) into `tools/nanogpt/out/`,
which `pixelate_enemy.py` then reads to build the final game frames.

The neutral/hero pose for each enemy is generated first without a
reference (it locks that enemy's identity); the remaining poses for that
enemy chain `--reference` off that first image so silhouette / palette /
proportions stay consistent across the sheet.

Model: `nano-banana-2` — same choice as the player pipeline, for the
same reason: it preserves character identity across independent
generations off a reference image. See lesson 8 in the SKILL.md.

Enemy design intent (matches `Assets/Art/Frontier/manifest.md`
readability rules and existing Frontier silhouettes):

* Bandit — upright human OUTLAW, clearly NOT the cowboy player: no
  wide-brim hat, no long duster, no red bandana. Uses a low bowler /
  round hat, an open vest over a bandolier, a bandana pulled up to
  cover mouth/nose (masked outlaw silhouette), rolled sleeves, single
  revolver. Palette biased away from player's blue jeans — grey/purple
  trousers instead — so bandit reads as hostile at a glance and can't
  be confused with the player mid-fight.
* Wildlife — LOW, WIDE, FORWARD-HEAVY quadruped. Coyote / mangy prairie
  wolf silhouette. Body clearly parallel to the ground, four legs, tail
  visible, long snout. Rust / timber body colors so it reads as "beast"
  vs the human bandit's cooler palette.
* Armadillo — LOW ROUNDED SHELL hazard. Wider/lower than wildlife, dark
  plum outline, rust/ochre shell, visible telegraph-gold fuse ember on
  top. Compact crawl/roll silhouette, never reads as flat debris.
* Snake — STATIONARY AMBUSH hazard. Hidden state is a dangerous dirt
  mound/crack marker with NO visible body. Visible states are upright
  cobra-like sidewinder S-curve silhouettes with rust/bone/sage accents
  and a telegraph-gold eye / rattle flash.

Prompt discipline learned from the cowboy work (all still applies):

* Explicit flat-color / no-gradient / no-anti-aliasing style block.
* Explicit transparent-background block with NO ground shadow / dust /
  scenery.
* Explicit compact composition — at 16x16 the enemy must fit in a very
  tight silhouette; the source prompt is worded to keep arms/legs close
  to the body so nothing clips after downscale.
* Right-facing side profile only (the renderer mirrors for left). The
  only exception is the snake's hidden mound state, where the snake body
  is intentionally not visible.

Usage:
    python tools/nanogpt/generate_enemy_frames.py           # all enemies
    python tools/nanogpt/generate_enemy_frames.py --enemy bandit
    python tools/nanogpt/generate_enemy_frames.py --state roll --enemy armadillo
    python tools/nanogpt/generate_enemy_frames.py --dry-run
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
ASPECT_RATIO = "1:1"

# ---------------------------------------------------------------------------
# Shared style / background blocks (identical to the player pipeline so
# the enemies drop into the same Frontier visual identity)
# ---------------------------------------------------------------------------

STYLE = """STYLE (critical):
- Chunky retro 16-bit game enemy sprite (SNES / Sega Genesis era).
- Flat solid color fills only. Absolutely NO gradients. NO soft shading. NO ambient occlusion. NO anti-aliasing. NO dithering.
- Hard clean 1-pixel-wide dark outlines around every shape.
- 4 solid color regions total, at most 5 including a tiny accent (small sprite — keep palette minimal).
- Frontier palette family: dark plum outline, rust, warm ochre, bone, timber. Muted, earthy, Western dust-gothic tone.
- Deliberate blocky pixel art, NOT smooth digital painting.
- Right-facing side profile — the enemy is turned to the viewer's RIGHT."""

BACKGROUND = """BACKGROUND (critical):
- Fully transparent background. Pure alpha zero.
- NO backdrop, NO ground plane, NO shadow, NO dust particles, NO smoke, NO speckles, NO scenery, NO text, NO labels. NOTHING except the enemy silhouette."""

# ---------------------------------------------------------------------------
# Bandit — human outlaw
# ---------------------------------------------------------------------------

BANDIT_CHARACTER = """CHARACTER (bandit — human outlaw, must NOT be confused with the player cowboy):
- Small stocky human silhouette, upright, standing on both feet.
- LOW ROUND BOWLER HAT (dark plum) — NOT a wide-brim cowboy hat. This is the single most important visual difference from the player.
- Face partially covered by a dirty grey-tan BANDANA pulled up over the mouth and nose (masked outlaw). Only a narrow eye slit visible.
- Dirty off-white or bone-colored long-sleeve SHIRT under an open dark brown VEST.
- Ammunition BANDOLIER strap of dust-tan color running diagonally across the chest, with a few tiny visible cartridges.
- Grey-purple TROUSERS (NOT blue jeans — do NOT copy the cowboy's blue color scheme).
- Scuffed brown BOOTS.
- One BROWN LEATHER HOLSTER on the right hip.
- CHUNKY sturdy proportions with a proportionally LARGE head — a stocky readable game-enemy silhouette. NOT thin, NOT spindly.
- Character silhouette fits a compact rectangle roughly as tall as wide (this enemy is intentionally shorter/stockier than the player)."""

BANDIT_POSES: dict[str, str] = {
    "patrol": (
        "Standing PATROL pose, feet planted shoulder-width apart on the ground, "
        "chin down slightly scanning the area, one hand resting on the hip near "
        "the holster, the other arm hanging at the side. Alert but not aggressive. "
        "The revolver is HOLSTERED, grip pointing UP out of the holster and "
        "barrel pointing DOWN into the holster, NOT drawn."
    ),
    "notice": (
        "SPOTTED-THE-PLAYER pose — surprised alertness. Head snapped upward, "
        "shoulders raised, one hand jerking toward the holstered gun as if "
        "about to draw. Feet still planted. The revolver is still HOLSTERED "
        "(grip up, barrel down into the holster), gun NOT yet drawn — this is "
        "the reaction moment BEFORE the shot. Body language reads as 'alarmed'."
    ),
    "attack": (
        "ATTACK / FIRING pose — the revolver is DRAWN and held in the right "
        "hand. STANCE IS COMPACT: feet close together and firmly planted, "
        "the shooting arm bent at the elbow with forearm horizontal, revolver "
        "at chest height pointing RIGHT (forward). The gun barrel is CLEARLY "
        "visible pointing right, extending only a short distance past the "
        "chest — NOT full arm-length away — so the whole enemy silhouette "
        "stays compact and readable at low resolution. The bandit's vest and "
        "shirt do NOT flare behind. Holster on right hip is EMPTY."
    ),
    "defeated": (
        "DEFEATED / KNOCKED-OUT pose — the bandit is LYING FLAT on the ground "
        "on their back, arms sprawled out to the sides, legs limp, hat fallen "
        "off and lying a couple of pixels away from the head. Body is fully "
        "HORIZONTAL, parallel to the ground. Eyes closed. Silhouette is a low "
        "flat horizontal shape wider than it is tall (roughly 2:1 aspect). "
        "This is the fainted / defeated state, not a mid-fall pose. Holster "
        "still on the hip, revolver holstered (grip up, barrel down)."
    ),
}


# ---------------------------------------------------------------------------
# Wildlife — feral quadruped
# ---------------------------------------------------------------------------

WILDLIFE_CHARACTER = """CHARACTER (wildlife — feral prairie predator, four-legged):
- COYOTE / mangy PRAIRIE WOLF body plan. Four legs, one visible tail, long pointed snout.
- Body is HORIZONTAL, roughly parallel to the ground. LOW to the ground. Wider than tall.
- Rust and timber-brown FUR palette with a paler bone-tan underbelly.
- Prominent SHOULDERS / haunches — forward-heavy, muscular front, silhouette communicates 'predator ready to lunge'.
- Bright warm-ochre EYE dot on the head — the only saturated highlight.
- Small dark ears pricked upward.
- Long tail sticking out horizontally BEHIND the body (to the left), NOT curled up.
- CHUNKY stocky proportions, thick limbs, NOT skinny.
- Silhouette clearly reads as 'four-legged beast', unmistakably NOT human, NOT the bandit.
- Right-facing side profile: head/snout on the right side of the image, tail on the left."""

WILDLIFE_POSES: dict[str, str] = {
    "patrol": (
        "PATROLLING pose — walking slowly along the ground. Body low and "
        "horizontal, all four feet on the ground, one front leg slightly "
        "forward mid-stride. Head level with the shoulders, snout forward. "
        "Tail hangs horizontally behind. Ears up. Silhouette is a wide, "
        "low, ground-hugging horizontal shape."
    ),
    "notice": (
        "SPOTTED-PREY pose — the beast has just noticed the player. Head "
        "snapped UP and forward, ears pointing straight up, front legs "
        "planted stiff, hackles slightly raised on the shoulders. Body "
        "still low and horizontal but chest lifted slightly off the ground "
        "in alertness. Tail stiff and horizontal. Reads as 'alarmed'."
    ),
    "lunge": (
        "MID-LUNGE ATTACK pose — the beast is leaping FORWARD-RIGHT off "
        "the ground. Front legs extended forward, hind legs pushed back "
        "and slightly airborne, jaw OPEN wide showing teeth, snout thrust "
        "forward. Tail streams straight out horizontally behind. Body is "
        "STRETCHED forward, longer than tall — a dynamic horizontal "
        "arrow-shape. Airborne but not high — hovering just above the "
        "ground. Reads unmistakably as 'attacking bite'."
    ),
    "defeated": (
        "DEFEATED / KILLED pose — the beast is LYING FLAT on its side on "
        "the ground, all four legs sprawled outward limp, head down, "
        "tongue slightly out, eyes closed, tail limp along the ground. "
        "Silhouette is a low horizontal shape (roughly 3:1 aspect) — flat "
        "against the ground. NOT mid-fall, NOT curled up."
    ),
}


# ---------------------------------------------------------------------------
# Dynamite armadillo — rolling ground hazard
# ---------------------------------------------------------------------------

ARMADILLO_CHARACTER = """CHARACTER (armadillo — explosive ground hazard):
- Very LOW, WIDE, ROUNDED ARMORED SHELL silhouette. Wider and lower than the wildlife quadruped.
- Compact desert armadillo body with a domed segmented shell and tiny tucked legs.
- Dark plum outline, RUST and warm OCHRE shell bands, a small BONE snout if visible.
- A short fuse stub on TOP of the shell with a bright telegraph-GOLD ember. Ember is tiny but clearly visible.
- Shell is the dominant read; legs are tiny supporting shapes only.
- Chunky readable hazard silhouette, NOT cute, NOT fluffy, NOT mammalian pet proportions.
- Must read as a living armored creature, NOT a loose tumbleweed, NOT a rock, NOT debris.
- Right-facing side profile: head/front on the RIGHT side of the image."""

ARMADILLO_POSES: dict[str, str] = {
    "patrol": (
        "PATROL / CRAWL pose — armadillo creeping along the ground on tiny legs. "
        "Body remains LOW and rounded, shell dominant, head slightly forward. "
        "Fuse ember on top is visible but calm. Silhouette is compact, wider than tall."
    ),
    "notice": (
        "NOTICE / SPOTTED-THE-PLAYER pose — the armadillo has stopped and turned its "
        "attention forward-right. Keep a CLEAR RIGHT-FACING SIDE PROFILE — do NOT face the camera "
        "front-on or 3/4. Head slightly raised, front planted, shell still low and compact. "
        "The top fuse has JUST CAUGHT with a brighter telegraph-GOLD ember. This is a tense "
        "warning pose before the charge, not yet rolling."
    ),
    "roll": (
        "ROLL / CHARGE pose — fast compact rolling armadillo silhouette, shell curled tighter "
        "into a dense rounded form with head tucked just enough to imply motion. IMPORTANT: "
        "do NOT depict the body lying flat or stretched parallel to the ground. The silhouette "
        "must still read as a compact rounded creature charging forward-right, with a visible "
        "fuse ember on top trailing slightly back. Low to the ground, dynamic, hazard-like."
    ),
}


# ---------------------------------------------------------------------------
# Sidewinder snake — ambush hazard
# ---------------------------------------------------------------------------

SNAKE_CHARACTER = """CHARACTER (sidewinder snake — ambush hazard):
- Slim desert sidewinder / cobra-like snake with an upright S-curve when exposed.
- Rust, bone, and muted sage body accents. Dark plum outline. Telegraph-GOLD eye or tiny rattle flash.
- Head wedge-shaped, neck capable of a hood-like widened strike profile, body coils tight and readable.
- Silhouette must read as a dangerous upright strike-ready snake, NOT a worm, NOT a decorative rope.
- When visible, keep the body compact and vertically readable for 16x16.
- Right-facing side profile: head on the RIGHT side of the image, body/coils trailing left/down."""

SNAKE_POSES: dict[str, str] = {
    "hidden": (
        "HIDDEN AMBUSH MARKER pose — only a small foreground dirt crack / dirt mound hazard marker is visible. "
        "The snake body is COMPLETELY NOT VISIBLE. No head, no tail, no coils above ground. "
        "The mound/crack must still read as an ACTIVE DANGEROUS foreground hazard marker, not ordinary decoration. "
        "Use a compact rust/timber dirt mound with a sharp crack opening and a tiny telegraph-GOLD glint inside the crack."
    ),
    "rise": (
        "RISE / EMERGING pose — the snake is pushing up from a dirt crack at ground level. "
        "Lower body still partly hidden by the dirt mound, upper body rising into an S-curve, "
        "head angled forward-right. This is the emergence phase, not yet fully exposed."
    ),
    "exposed": (
        "EXPOSED / STRIKE-READY pose — the snake is fully up in a compact upright S-curve / cobra-like silhouette. "
        "Head raised high, chest/neck widened slightly, coils rooted near the ground, rattle or tail hint behind. "
        "A tiny telegraph-GOLD eye or rattle flash is visible. This is the active attack-ready silhouette."
    ),
    "retreat": (
        "RETREAT pose — the snake is dropping back down into the dirt crack. "
        "Upper body lowered, head descending, much of the body already disappearing into the mound. "
        "Still clearly the same snake, but less exposed than the rise/exposed poses."
    ),
    "defeated": (
        "DEFEATED / SHOT pose — the snake has been killed and lies collapsed on the ground. "
        "Body slack and low, head down, curve broken into a limp fallen shape. "
        "Silhouette stays readable and compact, wider than tall, clearly no longer poised to strike."
    ),
}


ENEMIES: dict[str, dict[str, object]] = {
    "bandit": {
        "header": "A single BANDIT OUTLAW enemy character, isolated single subject centered in frame.",
        "character": BANDIT_CHARACTER,
        "poses": BANDIT_POSES,
        "reference_state": "patrol",
    },
    "wildlife": {
        "header": "A single WILD PREDATOR animal, isolated single subject centered in frame.",
        "character": WILDLIFE_CHARACTER,
        "poses": WILDLIFE_POSES,
        "reference_state": "patrol",
    },
    "armadillo": {
        "header": "A single DYNAMITE ARMADILLO enemy creature, isolated single subject centered in frame.",
        "character": ARMADILLO_CHARACTER,
        "poses": ARMADILLO_POSES,
        "reference_state": "patrol",
    },
    "snake": {
        "header": "A single SIDEWINDER SNAKE ambush hazard, isolated single subject centered in frame.",
        "character": SNAKE_CHARACTER,
        "poses": SNAKE_POSES,
        "reference_state": "exposed",
    },
}


# ---------------------------------------------------------------------------
# Prompt assembly
# ---------------------------------------------------------------------------


def _build_prompt(enemy: str, state: str) -> str:
    spec = ENEMIES.get(enemy)
    if spec is None:
        raise ValueError(f"Unknown enemy: {enemy}")
    character = str(spec["character"])
    poses = spec["poses"]
    assert isinstance(poses, dict)
    pose = poses[state]
    header = str(spec["header"])
    return (
        f"{header}\n\n"
        f"POSE: {pose}\n\n"
        f"{character}\n\n"
        f"{STYLE}\n\n"
        f"{BACKGROUND}"
    )


def _output_path(enemy: str, state: str) -> Path:
    return OUT / f"hero_{enemy}_{state}.png"


def _generate(enemy: str, state: str, reference: Path | None) -> Path:
    dest = _output_path(enemy, state)
    prompt = _build_prompt(enemy, state)
    print(f"[generate] {enemy}/{state} -> {dest.name}"
          + (f" (ref={reference.name})" if reference else " (no ref)"))
    generate_image.generate(
        prompt,
        MODEL,
        dest,
        resolution=RESOLUTION,
        quality=QUALITY,
        aspect_ratio=ASPECT_RATIO,
        n=1,
        seed=None,
        reference=reference,
    )
    return dest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--enemy", choices=tuple(ENEMIES), default=None)
    parser.add_argument("--state", default=None)
    parser.add_argument("--no-reference", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    enemies = [args.enemy] if args.enemy else list(ENEMIES)
    for enemy in enemies:
        spec = ENEMIES[enemy]
        poses = spec["poses"]
        assert isinstance(poses, dict)
        states_all_e = list(poses)
        states = [args.state] if args.state else states_all_e
        for state in states:
            if state not in poses:
                raise SystemExit(f"Unknown state for {enemy}: {state}")

        if args.dry_run:
            for s in states:
                print("=" * 70)
                print(f"ENEMY: {enemy}  STATE: {s}")
                print("=" * 70)
                print(_build_prompt(enemy, s))
            continue

        # Locked reference first (patrol/exposed depending on enemy), then chain
        # the rest off that reference image to preserve identity.
        order = states[:]
        reference_state = str(spec["reference_state"])
        if reference_state in order:
            order.remove(reference_state)
            order.insert(0, reference_state)

        ref: Path | None = None
        for s in order:
            use_ref = None if (args.no_reference or s == reference_state) else ref
            path = _generate(enemy, s, use_ref)
            if s == reference_state:
                ref = path


if __name__ == "__main__":
    main()
