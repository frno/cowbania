"""NanoGPT (https://nano-gpt.com) background-music generation helper.

Standalone, stdlib-only client for NanoGPT's OpenAI-compatible audio
endpoint (POST /api/v1/audio/speech), used with several different
text->music models to generate audition candidates for a loopable
techno-cowboy background track. Unlike generate_sfx.py, this script does
NOT pick a winner -- it generates one candidate per model, names each
output file after the model that produced it, and leaves the choice to
a human listening pass.

Auth: reads the API key from the NANOGPT_API_KEY environment variable, or
falls back to the gitignored tools/nanogpt/.secret/api_key.txt file. NEVER
hardcode the key here or pass it on the command line.

Requires ffmpeg on PATH (or pass --ffmpeg with a full path) to decode the
provider's MP3 response into a WAV for easy local auditioning.

Usage:
    python tools/nanogpt/generate_music.py --list-models
    python tools/nanogpt/generate_music.py --all --out-dir out/music
    python tools/nanogpt/generate_music.py --model ACE-Step-v1.5-Turbo --out-dir out/music
"""

from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

API_BASE = "https://nano-gpt.com/api"
_SECRET_FILE = Path(__file__).resolve().parent / ".secret" / "api_key.txt"

# Locked prompt: techno + spaghetti-western fusion, explicitly instrumental
# and loop-friendly (steady tempo, no big dynamic swells, no cold open/
# fade so the loop point is clean).
MUSIC_PROMPT = (
    "Instrumental techno cowboy fusion track for a western action video game, "
    "seamless perfect loop. Driving four-on-the-floor techno beat at a steady "
    "128 BPM, punchy analog synth bassline, distorted twangy reverb-drenched "
    "electric guitar with old-west film-score slide licks, a harmonica-like "
    "synth lead melody playing a dusty desert tune, cowbell and tambourine "
    "percussion layered over the techno groove, dusty saloon-meets-nightclub "
    "atmosphere, driving and energetic throughout with consistent energy and "
    "no big builds or drops, no vocals, no lyrics, no spoken word, "
    "no long intro, no fade in, no fade out, constant tempo and groove so "
    "the start and end bars match for looping."
)

# Each candidate: (model id, short label used in the output filename).
# Deliberately spans cheap/fast to premium/flagship so the user has a real
# quality range to choose from -- cost is not a constraint for this pass.
# (ACE-Step-v1.5-Turbo/Base returned a persistent 502 "Music generation
# service error" from NanoGPT's backend as of 2026-09-20 -- swapped for the
# older ACE-Step / ACE-Step-1.5 ids, which are healthy.)
MODELS: list[tuple[str, str]] = [
    ("ACE-Step", "ACE-Step"),
    ("ACE-Step-1.5", "ACE-Step-1.5"),
    ("Minimax-Music-2.6", "MiniMax-Music-2.6"),
    ("google/lyria-3-pro/music", "Google-Lyria-3-Pro"),
    ("mureka-ai/mureka-v9/generate-bgm", "Mureka-v9-BGM"),
    ("elevenlabs/music/v2.5", "ElevenLabs-Music-v2.5"),
]

# Requested duration in seconds for the generated candidate. Models that
# don't expose a duration parameter (checked via /v1/audio-models
# supported_parameters) simply ignore the field.
REQUEST_DURATION_SECONDS = 60.0

# Style tags for ACE-Step family (required by that model). Extra fields like
# this, `prompt`, and `lyrics` are harmless no-ops for models that don't use
# them (verified empirically against Lyria/ElevenLabs), so they're always
# included rather than special-cased per model.
STYLE_TAGS = "techno, western, spaghetti-western, synthwave, instrumental, driving, cowbell, 128bpm"
INSTRUMENTAL_LYRICS = "[instrumental]"

TARGET_RATE = 44100
TARGET_CHANNELS = 2
TARGET_BITS = 16


def _api_key() -> str:
    import os

    key = os.environ.get("NANOGPT_API_KEY")
    if not key and _SECRET_FILE.exists():
        key = _SECRET_FILE.read_text(encoding="utf-8").strip()
    if not key:
        print(
            "ERROR: no API key found. Set NANOGPT_API_KEY as an environment variable, "
            f"or write it (and only it) as the sole contents of {_SECRET_FILE}.",
            file=sys.stderr,
        )
        sys.exit(1)
    return key


def _request(method: str, url: str, *, body: dict | None = None) -> dict:
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    req.add_header("x-api-key", _api_key())
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        print(f"ERROR: HTTP {exc.code} from NanoGPT API: {detail}", file=sys.stderr)
        sys.exit(1)


def list_models() -> None:
    result = _request("GET", f"{API_BASE}/v1/audio-models")
    models = [m for m in result.get("data", []) if m.get("category") == "audio_music"]
    print(f"{len(models)} music models available:\n")
    for m in models:
        print(f"  {m['id']:<45} {m['name']:<30} {m.get('pricing')}")


def _submit(prompt: str, model: str) -> str:
    """Submit a music generation job. Returns the audio URL once complete."""
    body: dict = {
        "model": model,
        "input": prompt,
        "prompt": prompt,  # required by some providers (e.g. Mureka BGM) in addition to `input`
        "voice": "alloy",  # ignored for music models; some clients require it
        "duration_seconds": REQUEST_DURATION_SECONDS,
        "duration": REQUEST_DURATION_SECONDS,  # ACE-Step family uses `duration`, not `duration_seconds`
        "style_tags": STYLE_TAGS,  # required by ACE-Step family
        "tags": STYLE_TAGS,
        "lyrics": INSTRUMENTAL_LYRICS,  # required by MiniMax/ACE-Step-1.5 to force instrumental
    }
    result = _request("POST", f"{API_BASE}/v1/audio/speech", body=body)

    status = result.get("status")
    if status != "pending":
        print(f"ERROR: unexpected submit response: {result}", file=sys.stderr)
        sys.exit(1)

    run_id = result["runId"]
    # The submit response reports the fully-qualified provider model id used
    # for polling -- this is NOT always the same string as the request model
    # id (see the SFX pipeline skill for the same quirk with ElevenLabs SFX).
    status_model = result.get("model", model)
    print(f"  submitted runId={run_id}, polling (this can take a few minutes for music)...")

    for attempt in range(200):
        qs = urllib.parse.urlencode({"runId": run_id, "model": status_model})
        req = urllib.request.Request(f"{API_BASE}/tts/status?{qs}")
        req.add_header("x-api-key", _api_key())
        try:
            with urllib.request.urlopen(req, timeout=30) as resp:
                data = json.loads(resp.read().decode("utf-8"))
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", errors="replace")
            print(f"ERROR: HTTP {exc.code} while polling: {detail}", file=sys.stderr)
            sys.exit(1)

        if data.get("status") == "completed" and data.get("audioUrl"):
            return data["audioUrl"]
        if data.get("status") == "error":
            print(f"ERROR: generation failed: {data}", file=sys.stderr)
            sys.exit(1)
        time.sleep(3)

    print("ERROR: polling timed out after 10 minutes", file=sys.stderr)
    sys.exit(1)


def _download(url: str, dest: Path) -> None:
    with urllib.request.urlopen(url, timeout=180) as resp:
        dest.write_bytes(resp.read())


def _find_ffmpeg(explicit: str | None) -> str:
    if explicit:
        return explicit
    found = shutil.which("ffmpeg")
    if found:
        return found
    import os

    candidate_root = Path(os.environ.get("LOCALAPPDATA", "")) / "Microsoft" / "WinGet" / "Packages"
    if candidate_root.is_dir():
        for child in candidate_root.glob("Gyan.FFmpeg_*"):
            for exe in child.rglob("ffmpeg.exe"):
                return str(exe)
    print(
        "ERROR: ffmpeg not found on PATH. Install it (e.g. `winget install Gyan.FFmpeg`) "
        "or pass --ffmpeg <path-to-ffmpeg.exe>.",
        file=sys.stderr,
    )
    sys.exit(1)


def _decode_to_wav(ffmpeg: str, src: Path, dest: Path) -> None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    cmd = [
        ffmpeg, "-y", "-i", str(src),
        "-ac", str(TARGET_CHANNELS),
        "-ar", str(TARGET_RATE),
        "-sample_fmt", "s16",
        str(dest),
    ]
    proc = subprocess.run(cmd, capture_output=True, text=True)
    if proc.returncode != 0:
        print(f"ERROR: ffmpeg decode failed for {src}:\n{proc.stderr}", file=sys.stderr)
        sys.exit(1)


def _safe_label(label: str) -> str:
    return re.sub(r"[^A-Za-z0-9._-]+", "-", label)


def generate_one(model: str, label: str, *, out_dir: Path, ffmpeg: str) -> Path:
    safe = _safe_label(label)
    print(f"Generating techno-cowboy loop candidate from {model} ...")
    audio_url = _submit(MUSIC_PROMPT, model)
    raw_ext = Path(urllib.parse.urlparse(audio_url).path).suffix or ".mp3"
    raw_path = out_dir / f"_raw_{safe}{raw_ext}"
    _download(audio_url, raw_path)
    final_path = out_dir / f"Music_TechnoCowboy_{safe}.wav"
    _decode_to_wav(ffmpeg, raw_path, final_path)
    print(f"  saved: {final_path}")
    return final_path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--list-models", action="store_true", help="List available music models and exit.")
    parser.add_argument("--model", help="Generate a single model by id (must be a key in MODELS or any valid music model id).")
    parser.add_argument("--all", action="store_true", help="Generate one candidate from every model in MODELS.")
    parser.add_argument("--out-dir", type=Path, default=Path("out/music"), help="Output directory for generated WAVs.")
    parser.add_argument("--ffmpeg", default=None, help="Explicit path to ffmpeg.exe if not on PATH.")
    args = parser.parse_args()

    if args.list_models:
        list_models()
        return

    if not args.model and not args.all:
        parser.error("--model MODEL_ID or --all is required unless --list-models is given.")

    ffmpeg = _find_ffmpeg(args.ffmpeg)
    args.out_dir.mkdir(parents=True, exist_ok=True)

    if args.all:
        for model_id, label in MODELS:
            generate_one(model_id, label, out_dir=args.out_dir, ffmpeg=ffmpeg)
    else:
        label = next((lbl for mid, lbl in MODELS if mid == args.model), args.model)
        generate_one(args.model, label, out_dir=args.out_dir, ffmpeg=ffmpeg)


if __name__ == "__main__":
    main()
