"""Generate grand Cowbania title-theme candidates with NanoGPT."""

from __future__ import annotations

import importlib.util
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parent / "generate_music.py"
spec = importlib.util.spec_from_file_location("cowbania_music", MODULE_PATH)
music = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(music)

MODEL = "elevenlabs/music/v2.5"
BASE = (
    "Instrumental grand title theme for the same techno-cowboy western action game as a driving 128 BPM gameplay track. "
    "Keep the recognizable dusty musical identity: analog synth bass pulse, twangy electric guitar, harmonica-like synth motif, "
    "cowbell and dry electronic percussion. Make it more majestic and cinematic with wide low brass, bowed desert strings, "
    "deep tom drums and spacious frontier atmosphere. Begin in dramatic half-time, then reveal the familiar 128 BPM pulse beneath it. "
    "Heroic but restrained, ominous desert scale, memorable simple motif, no vocals, no lyrics, no spoken word, no choir, "
    "no abrupt ending, no fade in, no fade out, steady final bars suitable for a seamless game title loop."
)

VARIATIONS = {
    "A_BrassFrontier": "Emphasize noble low brass and huge frontier drums while keeping guitar and synth clearly audible.",
    "B_DesertStrings": "Emphasize sweeping bowed strings and distant harmonica color over the techno pulse, with restrained brass.",
    "C_TechnoMonument": "Emphasize monumental analog synth and tom rhythm, with twang guitar carrying the western melody.",
}


def main() -> None:
    out = Path("tools/nanogpt/out/title-music")
    out.mkdir(parents=True, exist_ok=True)
    ffmpeg = music._find_ffmpeg(None)
    original = music.MUSIC_PROMPT
    original_duration = music.REQUEST_DURATION_SECONDS
    try:
        music.REQUEST_DURATION_SECONDS = 60.0
        for label, variation in VARIATIONS.items():
            music.MUSIC_PROMPT = f"{BASE} {variation}"
            music.generate_one(MODEL, f"Title-{label}", out_dir=out, ffmpeg=ffmpeg)
    finally:
        music.MUSIC_PROMPT = original
        music.REQUEST_DURATION_SECONDS = original_duration


if __name__ == "__main__":
    main()