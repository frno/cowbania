"""NanoGPT (https://nano-gpt.com) sound-effect generation helper.

Standalone, stdlib-only client for NanoGPT's OpenAI-compatible audio
endpoint (POST /api/v1/audio/speech), used with a dedicated SFX model
(ElevenLabs Sound Effects v2 by default) to generate short game sound
effects, then converts + trims them into the uncompressed 16-bit PCM WAV
format required by Cowbania.Host's `ManagedPcmWav` reader (see
src/Cowbania.Host/Audio/ManagedPcmWav.cs).

Auth: reads the API key from the NANOGPT_API_KEY environment variable, or
falls back to the gitignored tools/nanogpt/.secret/api_key.txt file. NEVER
hardcode the key here or pass it on the command line.

Requires ffmpeg on PATH (or pass --ffmpeg with a full path) for the
mp3 -> PCM WAV conversion step; the SFX model does not return raw WAV
directly.

Usage:
    python tools/nanogpt/generate_sfx.py --list-models
    python tools/nanogpt/generate_sfx.py --all --out-dir out/sfx
    python tools/nanogpt/generate_sfx.py --name Shooting --out-dir out/sfx
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import wave
from array import array
from pathlib import Path

API_BASE = "https://nano-gpt.com/api"
DEFAULT_MODEL = "elevenlabs/sound-effects/v2"
# The async ticket returned by /v1/audio/speech reports this fully-qualified
# provider id; /tts/status must be polled with that exact value, not the
# short model id used in the request body.
STATUS_MODEL_ALIASES = {
    "elevenlabs/sound-effects/v2": "fal-ai/elevenlabs/sound-effects/v2",
}

_SECRET_FILE = Path(__file__).resolve().parent / ".secret" / "api_key.txt"

# Locked prompt set matching the runtime SFX filenames.
# Kept short/specific per lesson: describe transient shape + explicitly
# exclude music/reverb/voice so the model returns a clean, game-usable hit
# rather than a musical sting.
SFX_PROMPTS: dict[str, str] = {
    "Shooting": (
        "a single sharp revolver gunshot crack, dry close-up recording, "
        "punchy transient with a short natural decay, no echo, no reverb "
        "tail, no music, no voice"
    ),
    "Reload": (
        "a revolver cylinder reload, two crisp metallic clicks and a "
        "cylinder snap shut, dry mechanical foley, no music, no voice"
    ),
    "Pickup": (
        "a short bright arcade pickup chime, two ascending notes, cheerful "
        "and clean, no music bed, no voice"
    ),
    "Jump": (
        "a quick upward whoosh spring boing for a platformer jump, light "
        "and snappy, no music, no voice"
    ),
    "Dash": (
        "a quick sharp air whoosh dash swipe, fast and light, no music, "
        "no voice"
    ),
    "Damage": (
        "a short dull impact thud with a pained grunt, no words, no music, "
        "dry close-up foley"
    ),
    "ArmorRicochet": (
        "loud cartoon metal shield clang, one hard impact followed by a short bright "
        "ricochet ring, punchy arcade game sound, immediate full volume, dry and clean, "
        "no silence, no ambience, no music, no voice"
    ),
    "BanditHit": (
        "one loud crisp video game enemy hit, bullet striking a thick leather coat with a dry punch and cloth snap, "
        "sound begins immediately, single impact only, no voice, no scream, no music, no reverb, no silence"
    ),
    "BanditDeath": (
        "one short subdued outlaw defeat sound, boots and leather coat dropping onto dry dirt, restrained soft thud, "
        "immediate onset, no voice, no scream, no melody, no reverb"
    ),
    "WildlifeHit": (
        "one loud crisp video game enemy hit, blunt impact against thick animal hide with a low organic thump, "
        "sound begins immediately, single impact only, no animal cry, no voice, no music, no reverb, no silence"
    ),
    "WildlifeDeath": (
        "one short subdued desert animal defeat sound, light body settling into brush and dust, soft rustle and thud, "
        "no animal cry, no voice, no melody, no reverb"
    ),
    "ArmadilloHit": (
        "one loud crisp video game enemy hit, wooden mallet striking a hard hollow armadillo shell, low chunky clack, "
        "sound begins immediately, single impact only, no bright ring, no voice, no music, no reverb, no silence"
    ),
    "ArmadilloDeath": (
        "one short heavy armadillo shell collapse onto dry dirt, low hollow clack with a tiny dust tail, restrained, "
        "no animal cry, no voice, no melody, no reverb"
    ),
    "SidewinderHit": (
        "one short audible arcade impact on snake scales, crisp dry scale flick and sand tap at immediate full volume, "
        "restrained but clearly heard, no hiss, no voice, no music, no reverb, no silence"
    ),
    "SidewinderDeath": (
        "one short subdued sidewinder defeat sound, a brief dry rattle settling into sand, soft and restrained, "
        "no hiss, no animal cry, no melody, no reverb"
    ),
}

# Target format matching the existing placeholder WAV files exactly.
TARGET_RATE = 44100
TARGET_CHANNELS = 1
TARGET_BITS = 16

# Per-sound max duration after silence-trim, tuned by ear/envelope for how
# punchy vs. tailed each sound should read in actual gameplay. Falls back to
# --max-duration (default 1.2s) for anything not listed here.
MAX_DURATION_OVERRIDES: dict[str, float] = {
    "Jump": 0.45,
    "Dash": 0.4,
    "Pickup": 0.5,
    "Shooting": 1.0,
    "Reload": 1.1,
    "Damage": 0.8,
    "ArmorRicochet": 0.45,
    "BanditHit": 0.30,
    "BanditDeath": 0.48,
    "WildlifeHit": 0.30,
    "WildlifeDeath": 0.48,
    "ArmadilloHit": 0.30,
    "ArmadilloDeath": 0.48,
    "SidewinderHit": 0.30,
    "SidewinderDeath": 0.48,
}


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


def _request(method: str, url: str, *, body: dict | None = None, headers: dict | None = None) -> tuple[dict, dict]:
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    req.add_header("x-api-key", _api_key())
    for k, v in (headers or {}).items():
        req.add_header(k, v)
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            return json.loads(resp.read().decode("utf-8")), dict(resp.headers)
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        print(f"ERROR: HTTP {exc.code} from NanoGPT API: {detail}", file=sys.stderr)
        sys.exit(1)


def list_models() -> None:
    req = urllib.request.Request(f"{API_BASE}/v1/audio-models?type=all&detailed=true")
    with urllib.request.urlopen(req, timeout=60) as resp:
        result = json.loads(resp.read().decode("utf-8"))
    models = result.get("data", [])
    print(f"{len(models)} audio models available:\n")
    for m in models:
        caps = m.get("capabilities", {})
        flags = ",".join(k for k, v in caps.items() if v)
        print(f"  {m['id']}  [{flags}]")


def _submit(prompt: str, model: str, *, duration_seconds: float) -> str:
    """Submit an SFX generation job. Returns the audio URL once complete.

    duration_seconds constrains the upstream ElevenLabs SFX generation to a
    single focused take. Without it, the model can return several minutes-
    worth of padding containing multiple unrelated candidate variations
    scattered across a long clip (observed: 3 separate "damage" hits, or
    ~8 scattered reload clicks, in one 5s response) which a simple
    first-loud-window trim then latches onto the wrong one. See
    docs/pipelines/NANOGPT_AUDIO.md.
    """
    body = {
        "model": model,
        "input": prompt,
        "response_format": "wav",
        "duration_seconds": duration_seconds,
    }
    result, _ = _request("POST", f"{API_BASE}/v1/audio/speech", body=body)

    status = result.get("status")
    if status != "pending":
        print(f"ERROR: unexpected submit response: {result}", file=sys.stderr)
        sys.exit(1)

    run_id = result["runId"]
    status_model = STATUS_MODEL_ALIASES.get(model, model)
    print(f"  submitted runId={run_id}, polling...")

    for attempt in range(60):
        qs = urllib.parse.urlencode({"runId": run_id, "model": status_model})
        req = urllib.request.Request(f"{API_BASE}/tts/status?{qs}")
        req.add_header("x-api-key", _api_key())
        with urllib.request.urlopen(req, timeout=30) as resp:
            data = json.loads(resp.read().decode("utf-8"))

        if data.get("status") == "completed" and data.get("audioUrl"):
            return data["audioUrl"]
        if data.get("status") == "error":
            print(f"ERROR: generation failed: {data}", file=sys.stderr)
            sys.exit(1)
        time.sleep(3)

    print("ERROR: polling timed out after 180s", file=sys.stderr)
    sys.exit(1)


def _download(url: str, dest: Path) -> None:
    with urllib.request.urlopen(url, timeout=120) as resp:
        dest.write_bytes(resp.read())


def _find_ffmpeg(explicit: str | None) -> str:
    if explicit:
        return explicit
    found = shutil.which("ffmpeg")
    if found:
        return found
    # Common winget install location as a last-resort fallback.
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
    """Decode to mono/44100Hz/16-bit PCM WAV with no trimming; trimming is
    done separately in Python where the envelope logic is inspectable/
    debuggable (see the reload-truncation lesson in the SFX pipeline skill)."""
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


def _trim_silence(
    raw_wav: Path,
    dest: Path,
    *,
    max_duration: float,
    start_threshold_db: float = -34.0,
    tail_threshold_db: float = -45.0,
    tail_gap_s: float = 0.3,
    pre_roll_s: float = 0.01,
    fade_s: float = 0.05,
) -> None:
    """Find the real sound region by peak-amplitude envelope (not ffmpeg's
    stateful silenceremove, which mis-triggered on models that prepend a
    quiet room-tone lead-in before the actual sound; see
    docs/pipelines/NANOGPT_AUDIO.md).

    - start: first window whose peak exceeds start_threshold_db (loud enough
      to be unambiguously "the sound", skipping quiet lead-in noise).
    - end: walks forward from start, extending through brief quiet gaps
      (e.g. the pause between two reload clicks) as long as they're shorter
      than tail_gap_s; stops at the first gap >= tail_gap_s, or max_duration.
    """
    with wave.open(str(raw_wav), "rb") as w:
        rate = w.getframerate()
        n = w.getnframes()
        data = w.readframes(n)
    samples = array("h", data)

    window = max(1, int(rate * 0.01))  # 10ms windows
    start_threshold = 32768 * (10 ** (start_threshold_db / 20))
    tail_threshold = 32768 * (10 ** (tail_threshold_db / 20))

    def window_peak(i: int) -> int:
        chunk = samples[i:i + window]
        return max((abs(s) for s in chunk), default=0)

    n_windows = (len(samples) + window - 1) // window
    start_idx = None
    for wi in range(n_windows):
        if window_peak(wi * window) >= start_threshold:
            start_idx = wi
            break
    if start_idx is None:
        print(f"WARNING: no signal above {start_threshold_db}dB found in {raw_wav}; keeping full clip.", file=sys.stderr)
        start_idx = 0

    max_windows = int((max_duration * rate) / window)
    gap_windows = max(1, int((tail_gap_s * rate) / window))
    end_idx = start_idx
    quiet_run = 0
    for wi in range(start_idx, min(n_windows, start_idx + max_windows)):
        if window_peak(wi * window) >= tail_threshold:
            end_idx = wi
            quiet_run = 0
        else:
            quiet_run += 1
            if quiet_run >= gap_windows:
                break

    start_sample = max(0, start_idx * window - int(pre_roll_s * rate))
    end_sample = min(len(samples), (end_idx + 1) * window)
    trimmed = array("h", samples[start_sample:end_sample])

    fade_len = min(len(trimmed), int(fade_s * rate))
    if fade_len > 0:
        for i in range(fade_len):
            factor = i / fade_len
            idx = len(trimmed) - fade_len + i
            trimmed[idx] = int(trimmed[idx] * factor)

    dest.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(dest), "wb") as out:
        out.setnchannels(TARGET_CHANNELS)
        out.setsampwidth(TARGET_BITS // 8)
        out.setframerate(rate)
        out.writeframes(trimmed.tobytes())


def generate_one(name: str, *, model: str, out_dir: Path, ffmpeg: str, max_duration: float | None) -> Path:
    prompt = SFX_PROMPTS[name]
    effective_max = max_duration if max_duration is not None else MAX_DURATION_OVERRIDES.get(name, 1.2)
    # Ask the model for roughly 2x the trimmed target (min 1.5s) so it has
    # room for a natural lead-in/decay without falling back to a long
    # padded multi-take clip.
    request_duration = max(1.5, min(effective_max * 2.5, 4.0))
    print(f"Generating SFX_{name}... (max {effective_max}s, requesting {request_duration}s)")
    audio_url = _submit(prompt, model, duration_seconds=request_duration)
    raw_path = out_dir / f"_raw_{name}{Path(urllib.parse.urlparse(audio_url).path).suffix or '.mp3'}"
    _download(audio_url, raw_path)
    decoded_path = out_dir / f"_decoded_{name}.wav"
    _decode_to_wav(ffmpeg, raw_path, decoded_path)
    final_path = out_dir / f"SFX_{name}.wav"
    _trim_silence(decoded_path, final_path, max_duration=effective_max)
    print(f"  saved: {final_path}")
    return final_path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--list-models", action="store_true", help="List available audio models and exit.")
    parser.add_argument("--name", choices=sorted(SFX_PROMPTS), help="Generate a single named SFX.")
    parser.add_argument("--all", action="store_true", help="Generate all 7 SFX.")
    parser.add_argument("--model", default=DEFAULT_MODEL, help=f"Audio model ID (default: {DEFAULT_MODEL}).")
    parser.add_argument("--out-dir", type=Path, default=Path("out/sfx"), help="Output directory for generated WAVs.")
    parser.add_argument("--ffmpeg", default=None, help="Explicit path to ffmpeg.exe if not on PATH.")
    parser.add_argument("--max-duration", type=float, default=None, help="Override max seconds to keep after silence trim for all sounds (default: per-sound tuned values).")
    args = parser.parse_args()

    if args.list_models:
        list_models()
        return

    if not args.name and not args.all:
        parser.error("--name NAME or --all is required unless --list-models is given.")

    ffmpeg = _find_ffmpeg(args.ffmpeg)
    args.out_dir.mkdir(parents=True, exist_ok=True)

    names = sorted(SFX_PROMPTS) if args.all else [args.name]
    for name in names:
        generate_one(name, model=args.model, out_dir=args.out_dir, ffmpeg=ffmpeg, max_duration=args.max_duration)


if __name__ == "__main__":
    main()
