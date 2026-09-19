"""NanoGPT (https://nano-gpt.com) image generation helper.

Standalone, stdlib-only client for NanoGPT's normalized Image API
(POST /api/v1/images). Used to generate reference art (e.g. sprite
concepts) that a human or pixel-art pass then adapts into game assets.

Auth: reads the API key from the NANOGPT_API_KEY environment variable.
NEVER hardcode the key here or pass it on the command line where it could
land in shell history / logs. Set it once per session:

    PowerShell:  $env:NANOGPT_API_KEY = "your-key-here"
    bash/zsh:    export NANOGPT_API_KEY="your-key-here"

Usage:
    python tools/nanogpt/generate_image.py --list-models
    python tools/nanogpt/generate_image.py \
        --prompt "pixel art cowboy, wide-brim hat, confident pose, 32x32, transparent background" \
        --model "openai/gpt-image-2.5/flare/text-to-image" \
        --output out/cowboy_concept.png \
        --resolution 1k --quality high
"""

from __future__ import annotations

import argparse
import base64
import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path

API_BASE = "https://nano-gpt.com/api/v1"
DEFAULT_MODEL = "openai/gpt-image-2.5/flare/text-to-image"

# Local, gitignored fallback for environments where the env var doesn't
# propagate across shells/processes. Never commit this file (see .gitignore).
_SECRET_FILE = Path(__file__).resolve().parent / ".secret" / "api_key.txt"


def _api_key() -> str:
    key = os.environ.get("NANOGPT_API_KEY")
    if not key and _SECRET_FILE.exists():
        key = _SECRET_FILE.read_text(encoding="utf-8").strip()
    if not key:
        print(
            "ERROR: no API key found. Set NANOGPT_API_KEY as an environment variable, "
            f"or write it (and only it) as the sole contents of {_SECRET_FILE} "
            "(never pass it as a CLI argument).",
            file=sys.stderr,
        )
        sys.exit(1)
    return key


def _request(method: str, path: str, *, body: dict | None = None, auth: bool = True) -> dict:
    url = f"{API_BASE}{path}"
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if auth:
        req.add_header("x-api-key", _api_key())
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        print(f"ERROR: HTTP {exc.code} from NanoGPT API: {detail}", file=sys.stderr)
        sys.exit(1)


def list_models() -> None:
    result = _request("GET", "/images/models", auth=False)
    models = result.get("data", [])
    print(f"{len(models)} image models available:\n")
    for m in models:
        caps = m.get("capabilities", {})
        flags = ",".join(k for k, v in caps.items() if v)
        print(f"  {m['id']}  [{flags}]")


def generate(
    prompt: str,
    model: str,
    output: Path,
    *,
    resolution: str | None,
    quality: str | None,
    aspect_ratio: str | None,
    n: int,
    seed: int | None,
    reference: Path | None = None,
) -> None:
    body: dict = {"model": model, "prompt": prompt, "n": n}
    if resolution:
        body["resolution"] = resolution
    if quality:
        body["quality"] = quality
    if aspect_ratio:
        body["aspect_ratio"] = aspect_ratio
    if seed is not None:
        body["seed"] = seed
    if reference is not None:
        ref_bytes = reference.read_bytes()
        ref_b64 = base64.b64encode(ref_bytes).decode("ascii")
        suffix = reference.suffix.lstrip(".") or "png"
        body["input_references"] = [
            {"type": "image_url", "image_url": {"url": f"data:image/{suffix};base64,{ref_b64}"}}
        ]

    result = _request("POST", "/images", body=body)
    items = result.get("data", [])
    if not items:
        print(f"ERROR: no images returned. Raw response: {result}", file=sys.stderr)
        sys.exit(1)

    output.parent.mkdir(parents=True, exist_ok=True)
    stem, suffix = output.stem, output.suffix or ".png"
    for i, item in enumerate(items):
        dest = output if len(items) == 1 else output.with_name(f"{stem}_{i}{suffix}")
        if "b64_json" in item:
            dest.write_bytes(base64.b64decode(item["b64_json"]))
        elif "url" in item:
            with urllib.request.urlopen(item["url"], timeout=120) as resp:
                dest.write_bytes(resp.read())
        else:
            print(f"WARNING: unrecognized image item shape, skipping: {item.keys()}", file=sys.stderr)
            continue
        print(f"Saved: {dest}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--list-models", action="store_true", help="List available image models and exit.")
    parser.add_argument("--prompt", help="Text prompt describing the image to generate.")
    parser.add_argument("--model", default=DEFAULT_MODEL, help=f"Image model ID (default: {DEFAULT_MODEL}).")
    parser.add_argument("--output", type=Path, help="Output PNG path.")
    parser.add_argument("--resolution", default=None, help="Output resolution (model-dependent, e.g. 1k/2k/4k).")
    parser.add_argument("--quality", default=None, help="Quality tier (model-dependent).")
    parser.add_argument("--aspect-ratio", dest="aspect_ratio", default=None, help="Aspect ratio (model-dependent).")
    parser.add_argument("--n", type=int, default=1, help="Number of images to generate.")
    parser.add_argument("--seed", type=int, default=None, help="Optional seed for reproducibility (model-dependent).")
    parser.add_argument("--reference", type=Path, default=None, help="Path to a reference image for image-to-image style guidance.")
    args = parser.parse_args()

    if args.list_models:
        list_models()
        return

    if not args.prompt or not args.output:
        parser.error("--prompt and --output are required unless --list-models is given.")

    generate(
        args.prompt,
        args.model,
        args.output,
        resolution=args.resolution,
        quality=args.quality,
        aspect_ratio=args.aspect_ratio,
        n=args.n,
        seed=args.seed,
        reference=args.reference,
    )


if __name__ == "__main__":
    main()
