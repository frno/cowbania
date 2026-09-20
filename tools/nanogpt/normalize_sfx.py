"""Peak-normalize existing 16-bit PCM WAV SFX up to a target loudness.

Root-cause context: `tools/nanogpt/generate_sfx.py`'s `_trim_silence` finds
and keeps the real sound region by amplitude threshold, but never adjusts
the *level* of what it keeps -- it only trims silence around whatever
loudness ElevenLabs Sound Effects v2 happened to return for that take. That
model returns wildly inconsistent absolute levels across categories/takes
(observed: -1.3 dBFS for `BanditDeath.wav` vs -31.7 dBFS for
`ArmadilloHit.wav`, a ~30 dB gap, i.e. the quiet one plays at roughly 1/30th
the amplitude). Because playback volume in `AudioEventBus.Play` is clamped
to [0, 1] (see `SoundEffect.Play(volume, pitch, pan)`), no in-game volume
knob can compensate for a source file that is already this quiet -- the fix
has to boost the PCM data itself.

This script scales each input file's samples by a linear gain so its peak
sample reaches `--target-dbfs` (default -3 dBFS), but only ever turns
sounds UP (gain >= 1); files already at or above the target are left
untouched so already-punchy sounds (e.g. `BanditDeath.wav`,
`WildlifeHit.wav`) are not attenuated. Clips after scaling to prevent
integer overflow/wraparound.

Usage:
    python tools/nanogpt/normalize_sfx.py Assets/Audio/SFX_ArmadilloHit.wav ...
    python tools/nanogpt/normalize_sfx.py --target-dbfs -3 Assets/Audio/SFX_*.wav
"""

from __future__ import annotations

import argparse
import math
import wave
from array import array
from pathlib import Path


def _peak(samples: array) -> int:
    return max((abs(s) for s in samples), default=0)


def normalize_file(path: Path, target_dbfs: float, dry_run: bool = False) -> None:
    with wave.open(str(path), "rb") as w:
        params = w.getparams()
        if w.getsampwidth() != 2:
            print(f"SKIP {path.name}: not 16-bit PCM (sampwidth={w.getsampwidth()})")
            return
        data = w.readframes(w.getnframes())

    samples = array("h", data)
    peak = _peak(samples)
    if peak == 0:
        print(f"SKIP {path.name}: silent file (peak=0)")
        return

    target_peak = 32767 * (10 ** (target_dbfs / 20))
    gain = target_peak / peak
    if gain <= 1.0:
        before_db = 20 * math.log10(peak / 32768)
        print(f"SKIP {path.name}: already at {before_db:.1f} dBFS peak (>= target {target_dbfs} dBFS)")
        return

    before_db = 20 * math.log10(peak / 32768)
    scaled = array("h", (max(-32768, min(32767, int(s * gain))) for s in samples))
    after_peak = _peak(scaled)
    after_db = 20 * math.log10(after_peak / 32768)
    print(f"{path.name}: {before_db:.1f} dBFS -> {after_db:.1f} dBFS (gain x{gain:.2f})")

    if dry_run:
        return

    with wave.open(str(path), "wb") as out:
        out.setparams(params)
        out.writeframes(scaled.tobytes())


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("files", nargs="+", type=Path, help="WAV files to normalize in place.")
    parser.add_argument("--target-dbfs", type=float, default=-3.0, help="Target peak level in dBFS (default -3.0).")
    parser.add_argument("--dry-run", action="store_true", help="Report what would change without writing.")
    args = parser.parse_args()

    for f in args.files:
        if not f.exists():
            print(f"SKIP {f}: not found")
            continue
        normalize_file(f, args.target_dbfs, args.dry_run)


if __name__ == "__main__":
    main()
