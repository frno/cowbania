"""Generate an alternate pixel-art Cowbania title film with NanoGPT.

Pipeline:
1. Nano Banana 2 creates eight 16:9 pixel-art keyframes.
2. Kling 3.0 Turbo Pro animates each keyframe for eight seconds.
3. ffmpeg slows and crossfades the shots into a two-minute loop, then
   downscales/upscales with nearest-neighbor sampling to restore hard pixels.

Job responses are persisted before polling so interrupted runs can resume
without duplicate generation charges.
"""

from __future__ import annotations

import base64
import importlib.util
import json
import subprocess
import time
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "out" / "title-video-pixel"
IMAGE_MODEL = "nano-banana-2"
VIDEO_MODEL = "kling-v3-turbo-pro"
SECRET_FILE = ROOT / ".secret" / "api_key.txt"

STYLE = (
    "16:9 cinematic pixel art title-screen keyframe, deliberate chunky 1990s 32-bit game pixels, "
    "nearest-neighbor hard edges, no antialiasing, no smooth painting, limited Cowbania palette of "
    "near-black plum, rust, ochre, lit sand, muted sage and dusk blue, dramatic long shadows, "
    "sparse dusty western composition with generous negative space in the center for a logo, "
    "NO text, NO logo, NO lettering, NO border, NO photorealism"
)

SCENES = [
    ("01_dust_moon", "A huge pale moon over an empty desert trail before dawn, one tumbleweed crossing, tiny drifting dust pixels."),
    ("02_telegraph", "A lonely telegraph pole and sagging wire, empty trail, three tumbleweeds moving slowly beneath it."),
    ("03_spurs", "Low close-up of a cowboy's weathered boots and gold spurs, long shadow, rust poncho hem moving slightly."),
    ("04_hat_down", "The same lone cowboy in black wide-brim hat, rust poncho and red neckerchief, medium shot, head lowered, eyes hidden by brim."),
    ("05_rattlesnake", "A sidewinder rattlesnake rises from sand beside a cattle skull, compact S-curve silhouette, restrained small rattle."),
    ("06_grin_grip", "The same cowboy lifts his chin, gives a small confident grin, and tightens one hand around his holstered revolver grip without drawing."),
    ("07_long_ride", "Very wide quiet desert view, distant rider crosses between mesas, windmill turns, dust devil dissolves in the distance."),
    ("08_bell_dust", "Tall timber trail bell at sunset, hanging rope sways, tumbleweed passes, dust grows until it fills the frame like the opening dawn haze."),
]


def key() -> str:
    return SECRET_FILE.read_text(encoding="utf-8").strip()


def request(method: str, url: str, body: dict | None = None) -> dict:
    req = urllib.request.Request(url, data=json.dumps(body).encode() if body else None, method=method)
    req.add_header("x-api-key", key())
    req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req, timeout=120) as response:
        return json.load(response)


def image_keyframe(name: str, prompt: str, reference_url: str | None) -> tuple[Path, str]:
    output = OUT / f"{name}.png"
    manifest = OUT / f"{name}.image.json"
    if manifest.exists() and output.exists():
        return output, json.loads(manifest.read_text())["url"]
    body: dict = {
        "model": IMAGE_MODEL,
        "prompt": f"{STYLE}. {prompt}",
        "n": 1,
        "resolution": "2k",
        "aspect_ratio": "16:9",
    }
    if reference_url:
        body["input_references"] = [{"type": "image_url", "image_url": {"url": reference_url}}]
    item = request("POST", "https://nano-gpt.com/api/v1/images", body)["data"][0]
    url = item["url"]
    output.write_bytes(urllib.request.urlopen(url, timeout=180).read())
    manifest.write_text(json.dumps({"url": url, "model": IMAGE_MODEL}, indent=2))
    return output, url


def submit_video(name: str, prompt: str, image_url: str) -> str:
    job = OUT / f"{name}.video.json"
    if job.exists():
        return json.loads(job.read_text())["runId"]
    result = request("POST", "https://nano-gpt.com/api/generate-video", {
        "model": VIDEO_MODEL,
        "prompt": (
            "Preserve the source image's exact chunky pixel-art style, palette and character design. "
            "Only subtle deliberate motion, fixed camera, no morphing, no new objects, no text. " + prompt
        ),
        "imageUrl": image_url,
        "duration": "8",
        "aspect_ratio": "16:9",
    })
    job.write_text(json.dumps(result, indent=2))
    return result["runId"]


def retrieve_video(name: str, run_id: str) -> Path | None:
    output = OUT / f"{name}.mp4"
    if output.exists():
        return output
    query = urllib.parse.urlencode({"requestId": run_id})
    payload = request("GET", f"https://nano-gpt.com/api/video/status?{query}")
    data = payload.get("data", payload)
    status = str(data.get("status", "")).upper()
    if status == "COMPLETED":
        url = data["output"]["video"]["url"]
        output.write_bytes(urllib.request.urlopen(url, timeout=180).read())
        return output
    if status in {"FAILED", "CANCELED", "CANCELLED"}:
        raise RuntimeError(f"{name} failed: {payload}")
    return None


def stitch() -> None:
    clips = [OUT / f"{name}.mp4" for name, _ in SCENES]
    command = ["ffmpeg", "-y"]
    for clip in clips:
        command.extend(["-i", str(clip)])
    filters = []
    for index in range(len(clips)):
        filters.append(
            f"[{index}:v]setpts=2.0*PTS,fps=12,scale=854:480:force_original_aspect_ratio=increase,"
            f"crop=854:480,scale=256:144:flags=neighbor,scale=1024:576:flags=neighbor,format=yuv420p[v{index}]"
        )
    previous = "v0"
    offset = 15.0
    for index in range(1, len(clips)):
        current = f"x{index}"
        filters.append(f"[{previous}][v{index}]xfade=transition=fade:duration=1:offset={offset:.1f}[{current}]")
        previous = current
        offset += 15.0
    command.extend([
        "-filter_complex", ";".join(filters), "-map", f"[{previous}]", "-an",
        "-c:v", "libx264", "-preset", "slow", "-crf", "20", "-pix_fmt", "yuv420p",
        "-movflags", "+faststart", str(OUT / "cowbania_title_pixel_loop.mp4"),
    ])
    subprocess.run(command, check=True)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    # Character shots chain from the first cowboy frame; environmental shots
    # remain independent so they preserve spacious, low-noise compositions.
    references: dict[str, str] = {}
    for name, prompt in SCENES:
        reference_url = references.get("cowboy") if name in {"04_hat_down", "06_grin_grip"} else None
        image, url = image_keyframe(name, prompt, reference_url)
        if name == "04_hat_down":
            references["cowboy"] = url
        run_id = submit_video(name, prompt, url)
        print(f"{name}: {run_id}")
        time.sleep(4)

    pending = True
    while pending:
        pending = False
        for name, _ in SCENES:
            job = json.loads((OUT / f"{name}.video.json").read_text())
            if retrieve_video(name, job["runId"]) is None:
                pending = True
        if pending:
            time.sleep(8)
    stitch()


if __name__ == "__main__":
    main()