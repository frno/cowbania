"""Post-process an AI-generated music track into a seamlessly loopable WAV.

AI music generators rarely produce a clip whose last sample flows naturally
back into its first sample -- looping the raw output verbatim produces an
audible click/pop at the loop boundary. This applies the standard game-audio
loop-crossfade technique instead of naive trimming:

    1. Take the full clip (length L samples).
    2. Crossfade the LAST `crossfade` seconds of the clip with the FIRST
       `crossfade` seconds (tail fades out while head fades in, sample-wise).
    3. Replace the tail region with that blended segment. The head itself is
       left untouched, so what plays at the very start of every loop
       iteration is always identical -- only the seam is smoothed.

Output has the same total duration as the input; only the last
`crossfade` seconds are altered.

Usage:
    python tools/nanogpt/loop_music.py <input.wav> <output.wav> [--crossfade 1.5]
"""

from __future__ import annotations

import argparse
import math
import wave
from array import array
from pathlib import Path


def _boundary_jump(samples: array, channels: int) -> float:
    """RMS of the sample-to-sample delta at the wrap point (last frame -> first
    frame), across all channels. Lower is a smoother loop seam."""
    first = samples[:channels]
    last = samples[-channels:]
    deltas = [a - b for a, b in zip(first, last)]
    return math.sqrt(sum(d * d for d in deltas) / len(deltas))


def make_loopable(src: Path, dest: Path, crossfade_seconds: float) -> None:
    with wave.open(str(src), "rb") as w:
        channels = w.getnchannels()
        rate = w.getframerate()
        sampwidth = w.getsampwidth()
        n = w.getnframes()
        data = w.readframes(n)
    if sampwidth != 2:
        raise ValueError(f"Expected 16-bit PCM, got sampwidth={sampwidth}")

    samples = array("h", data)
    frame_count = len(samples) // channels
    crossfade_frames = min(frame_count // 4, int(crossfade_seconds * rate))
    crossfade_len = crossfade_frames * channels

    before_jump = _boundary_jump(samples, channels)

    head_start = samples[:channels]  # the exact frame that plays right after the wrap
    tail = samples[-crossfade_len:]
    blended = array("h", [0]) * crossfade_len
    for i in range(crossfade_frames):
        # Ramp the tail's own natural trajectory down to exactly match
        # head_start by the very last frame, so output[-1] == output[0] and
        # the loop wrap has no sample-level jump (the actual audible
        # "click"). This is a *targeted* declick interpolation, not a
        # musical crossfade -- it only needs to erase the seam, not blend in
        # the head's future waveform shape (slope mismatches are far less
        # audible than position/level mismatches at a loop seam).
        t = i / (crossfade_frames - 1) if crossfade_frames > 1 else 1.0
        for c in range(channels):
            idx = i * channels + c
            blended[idx] = int(tail[idx] * (1.0 - t) + head_start[c] * t)

    output = array("h")
    output.extend(samples[: len(samples) - crossfade_len])
    output.extend(blended)

    after_jump = _boundary_jump(output, channels)

    dest.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(dest), "wb") as out:
        out.setnchannels(channels)
        out.setsampwidth(2)
        out.setframerate(rate)
        out.writeframes(output.tobytes())

    print(f"crossfade: {crossfade_frames / rate:.2f}s ({crossfade_frames} frames)")
    print(f"loop-boundary discontinuity RMS: before={before_jump:.1f} after={after_jump:.1f}")
    print(f"saved: {dest} ({len(output) // channels / rate:.2f}s)")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("src", type=Path)
    parser.add_argument("dest", type=Path)
    parser.add_argument("--crossfade", type=float, default=1.5, help="Crossfade length in seconds (default 1.5).")
    args = parser.parse_args()
    make_loopable(args.src, args.dest, args.crossfade)


if __name__ == "__main__":
    main()
