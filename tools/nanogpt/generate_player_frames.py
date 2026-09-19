"""Generate the 8 base pose PNGs for the Cowbania player sprite.

Produces one AI image per animation state into `tools/nanogpt/out/`,
which `pixelate_sprite.py` then reads to build the final 26 game frames.
The idle pose is generated first (no reference) and locks the visual
style; every other pose is generated with `--reference` pointing at the
idle image so hat / coat / palette / proportions stay consistent across
states.

Model: `nano-banana-2` (winner of the consistency bake-off; strong
image-to-image reference adherence — keeps face / skin tone / palette /
proportions locked across all 8 pose generations off the same idle
reference, which is the requirement that killed `seedream-v5.0-lite`
after the full-motion test). `seedream-v5.0-lite` still produces cleaner
native "chunky pixel art" one-shot output, but drifts on identity
across independent generations even with a reference. For character
sheets we need identity > per-frame style, and the downscale pipeline
handles nano-banana's softer output fine.

Key per-pose prompt discipline (learned):

* Holstered gun (any pose EXCEPT `shoot` and `reload`): describe explicit
  correct holster orientation — grip up, barrel DOWN into the holster,
  NOT pointing forward. AI models default to a "gun pointing forward
  from the hip" render otherwise which looks nonsensical.
* Shoot pose: keep the shooting stance COMPACT (arm partially extended,
  elbow bent, gun at chest height, feet close together) so the extended
  arm doesn't clip off the 32x32 canvas after fit-by-height.
* Every prompt reasserts the flat-pixel-art style block and the empty-
  background block, because the model doesn't remember them across
  independent generations even with `--reference`.

Usage:
    python tools/nanogpt/generate_player_frames.py           # all 8
    python tools/nanogpt/generate_player_frames.py --state shoot
    python tools/nanogpt/generate_player_frames.py --dry-run # print prompts, don't call API
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

# --- Shared prompt building blocks -----------------------------------------

CHARACTER = """CHARACTER (must be legible at a glance):
- Wide-brim brown cowboy hat with a darker hatband. Hat brim is moderate width, NOT oversized.
- Red bandana / neckerchief around the neck, hanging in front of the chest.
- Dark brown, near-black long duster coat that hangs straight down. NOT a cape, NOT flared.
- Brown vest visible under the open front of the coat.
- Brown leather gunbelt with visible holster on the right hip.
- Dark blue jeans and brown boots visible below the coat.
- Chunky sturdy thick-limbed proportions with a proportionally LARGE head — a stocky readable game-sprite silhouette. NOT thin, NOT spindly, NOT a tall skinny anime figure.
- Right-facing side profile — the character is turned to the viewer's RIGHT."""

STYLE = """STYLE (critical):
- Chunky retro 16-bit game sprite (SNES / Sega Genesis era).
- Flat solid color fills only. Absolutely NO gradients. NO soft shading. NO ambient occlusion. NO anti-aliasing. NO dithering.
- Hard clean 1-pixel-wide dark outlines around every shape.
- 6 to 8 solid color regions total. Deliberate blocky pixel art, NOT smooth digital painting.
- Character silhouette fits a tall narrow rectangle (roughly 3:5 aspect, taller than wide)."""

BACKGROUND = """BACKGROUND (critical):
- Fully transparent background. Pure alpha zero.
- NO backdrop, NO ground plane, NO shadow, NO dust particles, NO smoke, NO speckles, NO scenery, NO text, NO labels. NOTHING except the character silhouette."""

# Correct sidearm orientation. Reused wherever the gun is HOLSTERED.
HOLSTER_CORRECT = (
    "The revolver is HOLSTERED on the right hip. Holster orientation is realistic: "
    "the revolver grip points STRAIGHT UP out of the top of the holster, and the "
    "barrel points STRAIGHT DOWN into the holster. The barrel is NOT pointing "
    "forward and the gun is NOT drawn — it is sheathed inside the holster with "
    "only the grip and cylinder area visible above the belt line."
)

# Empty holster variant. Reused for `shoot` and `reload` where gun is drawn.
HOLSTER_EMPTY = (
    "The holster on the right hip is EMPTY — the revolver is currently drawn and "
    "held in the character's hand, not in the holster. The empty holster is "
    "clearly visible on the belt."
)


# --- Per-pose descriptions --------------------------------------------------


POSES: dict[str, dict[str, str]] = {
    "idle_side": {
        "filename": "hero_idle_side.png",
        "pose": (
            "Standing in a confident tough idle pose, feet close together, chin up, "
            "shoulders squared, arms relaxed at the sides. Not moving. "
            + HOLSTER_CORRECT
        ),
    },
    "run": {
        "filename": "hero_run.png",
        "pose": (
            "Mid-stride running to the right — one leg forward and slightly bent, "
            "the other leg pushing back behind, subtle forward lean of the torso "
            "(~15 degrees), arms swinging opposite the legs for balance. The coat "
            "hem lifts slightly from the motion but does NOT streamer horizontally. "
            "Head above torso above legs, character upright and readable. "
            + HOLSTER_CORRECT
        ),
    },
    "jump": {
        "filename": "hero_jump.png",
        "pose": (
            "Airborne at the peak of a jump. Both knees bent and tucked forward "
            "toward the chest, feet off the ground, torso upright and slightly "
            "forward, one hand holding the hat brim. Character silhouette is "
            "compact — arms and legs stay CLOSE to the body, not spread out. "
            + HOLSTER_CORRECT
        ),
    },
    "fall": {
        "filename": "hero_fall.png",
        "pose": (
            "Descending through mid-air after a jump. Body upright and vertical, "
            "coat tails hanging straight down under gravity, one arm extended "
            "slightly outward for balance. Feet pointed downward preparing to land. "
            "Compact silhouette, arms close to body. "
            + HOLSTER_CORRECT
        ),
    },
    "shoot": {
        "filename": "hero_shoot.png",
        "pose": (
            "Firing the revolver forward. The gun is DRAWN and held in the "
            "character's right hand. STANCE IS COMPACT: feet close together, "
            "elbow bent, forearm horizontal, revolver held at chest height and "
            "sticking out only a short distance in front of the chest — NOT full "
            "arm-length away from the body. Coat hangs straight down and does NOT "
            "flare behind. Character silhouette stays TALL AND NARROW (taller than "
            "wide), so the extended arm doesn't push the outline into a wide "
            "rectangle. The revolver silhouette is CLEAN AND UNMISTAKABLE — "
            "obvious barrel pointing right, trigger guard visible, one clear hand "
            "gripping it — reads as 'gun' at a glance. " + HOLSTER_EMPTY
        ),
    },
    "reload": {
        "filename": "hero_reload.png",
        "pose": (
            "Reloading the revolver. The gun is DRAWN and held in both hands close "
            "to the chest — the left hand cradling the cylinder, the right hand "
            "thumbing a bullet cartridge into a chamber. Head tilted down slightly "
            "looking at the gun. Upright compact stance, feet close together, coat "
            "hangs straight down. Both arms stay CLOSE to the torso — silhouette "
            "stays tall and narrow. " + HOLSTER_EMPTY
        ),
    },
    "hurt": {
        "filename": "hero_hurt.png",
        "pose": (
            "Recoiling backward from a hit, torso leaning back about 20 degrees, "
            "one hand raised toward the chest as if warding off the impact, feet "
            "planted but slightly staggered, hat tilted back a touch. Head above "
            "torso above legs — the character is still UPRIGHT and readable, NOT "
            "lying flat, NOT bent horizontally. " + HOLSTER_CORRECT
        ),
    },
    "dash": {
        "filename": "hero_dash.png",
        "pose": (
            "Dashing forward at high speed — strong forward lean of the torso "
            "(about 35-40 degrees), one leg pushing hard back, the other stepping "
            "forward, arms tucked and pumping, coat tails trailing back behind at "
            "an angle. Head is still ABOVE the torso which is still above the "
            "legs — the character is leaning forward but NOT lying flat and NOT "
            "parallel to the ground. " + HOLSTER_CORRECT
        ),
    },
}


def _build_prompt(pose_key: str) -> str:
    pose = POSES[pose_key]["pose"]
    return (
        "A single cowboy character, isolated single subject centered in frame.\n\n"
        f"POSE: {pose}\n\n"
        f"{CHARACTER}\n\n"
        f"{STYLE}\n\n"
        f"{BACKGROUND}"
    )


def _generate(pose_key: str, reference: Path | None) -> Path:
    dest = OUT / POSES[pose_key]["filename"]
    prompt = _build_prompt(pose_key)
    print(f"[generate] {pose_key} -> {dest.name}")
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
    parser.add_argument("--state", default=None, help="Only regenerate this state (default: all 8).")
    parser.add_argument("--no-reference", action="store_true", help="Do not chain --reference off idle_side (each pose generated independently).")
    parser.add_argument("--dry-run", action="store_true", help="Print prompts, do not call API.")
    args = parser.parse_args()

    keys = list(POSES) if args.state is None else [args.state]
    if args.state is not None and args.state not in POSES:
        raise SystemExit(f"Unknown state: {args.state}. Known: {list(POSES)}")

    if args.dry_run:
        for k in keys:
            print("=" * 70)
            print(f"POSE: {k}")
            print("=" * 70)
            print(_build_prompt(k))
        return

    # Always generate idle_side first if it's in the set — it locks style.
    order = keys[:]
    if "idle_side" in order:
        order.remove("idle_side")
        order.insert(0, "idle_side")

    ref: Path | None = None
    for k in order:
        use_ref = None if (args.no_reference or k == "idle_side") else ref
        path = _generate(k, use_ref)
        if k == "idle_side":
            ref = path


if __name__ == "__main__":
    main()
