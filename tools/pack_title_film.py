"""Pack an MP4 into Cowbania's dependency-free JPEG title-film stream."""

from __future__ import annotations

import argparse
import shutil
import struct
import subprocess
import tempfile
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--ffmpeg", default="ffmpeg")
    parser.add_argument("--width", type=int, default=512)
    parser.add_argument("--height", type=int, default=288)
    parser.add_argument("--fps", type=int, default=4)
    parser.add_argument("--quality", type=int, default=8)
    args = parser.parse_args()

    temp = Path(tempfile.mkdtemp(prefix="cowbania-title-"))
    try:
        subprocess.run([
            args.ffmpeg, "-y", "-i", str(args.input), "-an",
            "-vf", f"fps={args.fps},scale={args.width}:{args.height}:flags=lanczos",
            "-q:v", str(args.quality), str(temp / "%05d.jpg"),
        ], check=True)
        frames = sorted(temp.glob("*.jpg"))
        if not frames:
            raise RuntimeError("ffmpeg produced no title frames")
        args.output.parent.mkdir(parents=True, exist_ok=True)
        with args.output.open("wb") as output:
            output.write(b"CWVF")
            output.write(struct.pack("<IIII", args.width, args.height, args.fps, len(frames)))
            for frame in frames:
                data = frame.read_bytes()
                output.write(struct.pack("<I", len(data)))
                output.write(data)
        print(f"Packed {len(frames)} frames ({len(frames)/args.fps:.1f}s) to {args.output}")
    finally:
        shutil.rmtree(temp, ignore_errors=True)


if __name__ == "__main__":
    main()