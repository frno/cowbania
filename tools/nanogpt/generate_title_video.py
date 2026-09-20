"""Generate Cowbania's restrained title montage with NanoGPT video models.

Creates eight short 16:9 source scenes, downloads them, then uses ffmpeg to
slow, crossfade, and loop them into a two-minute silent title film. The source
MP4 is retained for review; `pack_title_film.py` creates the dependency-free
runtime frame stream used by MonoGame DesktopGL.

Auth follows the other NanoGPT tools: NANOGPT_API_KEY or the gitignored
tools/nanogpt/.secret/api_key.txt file.
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import time
import urllib.parse
import urllib.request
from pathlib import Path

API_BASE = "https://nano-gpt.com/api"
DEFAULT_MODEL = "bytedance-seedance-v1-pro-fast"
SECRET_FILE = Path(__file__).resolve().parent / ".secret" / "api_key.txt"

STYLE = (
    "restrained cinematic dusty western, 16:9 widescreen, muted sepia and charcoal palette, "
    "deep black shadows, sparse composition, subtle film grain, slow minimal motion, fixed camera, "
    "same lone cowboy wearing a black wide-brim hat, rust poncho and red neckerchief, "
    "no text, no logo, no subtitles, no border, no modern objects, no gunfire, no gore"
)

SCENES = [
    ("01_dawn", "Empty desert before sunrise. A long trail disappears between low mesas. Fine dust drifts sideways and one distant tumbleweed crosses slowly."),
    ("02_tumbleweeds", "Wide abandoned frontier trail at midday. Two tumbleweeds roll through alternating bands of shadow while an old telegraph wire sways slightly."),
    ("03_boots", "Low close shot of weathered cowboy boots standing still in dust. A long shadow stretches toward camera; coat hem and red neckerchief edge move gently in the wind."),
    ("04_looking_down", "Medium close shot of the lone cowboy beneath his wide hat, head lowered so the brim hides his eyes. He stands motionless, calm and watchful, dust moving behind him."),
    ("05_sidewinder", "Close shot of a sidewinder rattlesnake emerging from warm sand beside a dry cattle skull. It coils once and gives one small restrained rattle, not attacking."),
    ("06_grin_and_grip", "The same cowboy notices something below frame, slowly raises his chin enough to reveal a subtle confident grin, and calmly tightens one hand around the grip of his holstered revolver. He does not draw."),
    ("07_frontier_cutaways", "Quiet frontier still life: a windmill turns very slowly beyond a fence, dust crosses the road, a distant rider silhouette passes between mesas, telegraph wire hums in the breeze."),
    ("08_trail_bell", "Sunset at a tall timber trail bell with a hanging rope. Wind moves the rope slightly, a tumbleweed passes, and blowing dust gradually fills the frame until it resembles the pale dawn haze of the opening shot."),
]


def api_key() -> str:
    key = os.environ.get("NANOGPT_API_KEY")
    if not key and SECRET_FILE.exists():
        key = SECRET_FILE.read_text(encoding="utf-8").strip()
    if not key:
        raise SystemExit("NANOGPT_API_KEY or tools/nanogpt/.secret/api_key.txt is required")
    return key


def request_json(method: str, url: str, body: dict | None = None) -> dict:
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("x-api-key", api_key())
    req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req, timeout=90) as response:
        return json.loads(response.read().decode())


def generate(prompt: str, model: str, duration: int) -> str:
    result = request_json("POST", f"{API_BASE}/generate-video", {
        "model": model,
        "prompt": f"{STYLE}. {prompt}",
        "duration": str(duration),
        "resolution": "480p",
        "aspect_ratio": "16:9",
        "camera_fixed": True,
    })
    run_id = result.get("runId") or result.get("id")
    if not run_id:
        raise RuntimeError(f"Unexpected generation response: {result}")
    print(f"  submitted {run_id}")
    for _ in range(120):
        query = urllib.parse.urlencode({"requestId": run_id})
        status = request_json("GET", f"{API_BASE}/video/status?{query}")
        data = status.get("data", status)
        state = str(data.get("status", status.get("status", ""))).upper()
        if state == "COMPLETED" or state == "COMPLETED":
            return (data.get("output", {}).get("video", {}).get("url") or
                    data.get("videoUrl") or status.get("videoUrl"))
        if state in {"FAILED", "CANCELED", "CANCELLED"}:
            raise RuntimeError(f"Video generation failed: {status}")
        time.sleep(5)
    raise TimeoutError(f"Video generation timed out: {run_id}")


def download(url: str, path: Path) -> None:
    if not url:
        raise RuntimeError("Completed video response had no URL")
    with urllib.request.urlopen(url, timeout=180) as response:
        path.write_bytes(response.read())


def stitch(ffmpeg: str, clips: list[Path], output: Path) -> None:
    # Each 8-second source becomes 16 seconds. One-second dissolves leave a
    # quiet ~121-second montage, then the final dust shot dissolves naturally
    # back into the opening dawn when playback loops.
    command = [ffmpeg, "-y"]
    for clip in clips:
        command.extend(["-i", str(clip)])
    filters = []
    for index in range(len(clips)):
        filters.append(
            f"[{index}:v]setpts=2.0*PTS,fps=12,scale=854:480:force_original_aspect_ratio=increase,"
            f"crop=854:480,format=yuv420p[v{index}]"
        )
    previous = "v0"
    offset = 15.0
    for index in range(1, len(clips)):
        current = f"x{index}"
        filters.append(f"[{previous}][v{index}]xfade=transition=fade:duration=1:offset={offset:.1f}[{current}]")
        previous = current
        offset += 15.0
    command.extend([
        "-filter_complex", ";".join(filters),
        "-map", f"[{previous}]", "-an", "-c:v", "libx264", "-preset", "slow", "-crf", "22",
        "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(output),
    ])
    subprocess.run(command, check=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", default=DEFAULT_MODEL)
    parser.add_argument("--duration", type=int, default=8)
    parser.add_argument("--out-dir", type=Path, default=Path("tools/nanogpt/out/title-video"))
    parser.add_argument("--ffmpeg", default="ffmpeg")
    parser.add_argument("--stitch-only", action="store_true")
    args = parser.parse_args()
    args.out_dir.mkdir(parents=True, exist_ok=True)
    clips = []
    for name, scene in SCENES:
        path = args.out_dir / f"{name}.mp4"
        clips.append(path)
        if path.exists() or args.stitch_only:
            continue
        print(f"Generating {name}...")
        download(generate(scene, args.model, args.duration), path)
    if any(not clip.exists() for clip in clips):
        raise SystemExit("Cannot stitch: one or more source clips are missing")
    stitch(args.ffmpeg, clips, args.out_dir / "cowbania_title_loop.mp4")


if __name__ == "__main__":
    main()