---
name: "nanogpt-audio-pipeline"
description: "Use when generating or regenerating Cowbania sound effects with AI audio generation (NanoGPT / ElevenLabs Sound Effects v2). Do not load for music, voice, or image/sprite tasks."
domain: "audio-pipeline"
confidence: "medium"
source: "earned — SFX replacement (feature/cowboy-sprite-redesign)"
---

## Purpose

Generate game-ready, short one-shot sound effects with NanoGPT's ElevenLabs Sound Effects v2
model and land them as uncompressed 16-bit PCM WAV files the game's `ManagedPcmWav` reader can
load, without unusable/truncated/multi-take output. Captures what broke and what fixed it
during the first end-to-end SFX replacement run.

## Tooling

- `tools/nanogpt/generate_sfx.py` — stdlib-only client. Submits jobs to NanoGPT's
  `/v1/audio/speech` endpoint with model `elevenlabs/sound-effects/v2`, polls for completion,
  downloads the MP3, converts to PCM WAV via ffmpeg, then trims silence in Python.
  `--all` regenerates all 6 SFX (`Shooting`, `Reload`, `Pickup`, `Jump`, `Dash`, `Damage`);
  `--name <X>` regenerates one. Prompts and per-sound `MAX_DURATION_OVERRIDES` are locked in
  the script for reproducibility.
- Requires ffmpeg. `_find_ffmpeg()` checks PATH, an explicit `--ffmpeg` arg, then falls back to
  globbing the winget install path (`Gyan.FFmpeg_*` under
  `%LOCALAPPDATA%\Microsoft\WinGet\Packages`). New PowerShell processes in this environment do
  not immediately see winget's PATH update, so pass `--ffmpeg <full path>` explicitly if PATH
  lookup fails right after install.

## Format contract

`src/Cowbania.Host/Audio/ManagedPcmWav.cs` only accepts uncompressed 16-bit PCM WAV (mono or
stereo), valid RIFF/WAVE/fmt/data chunks. ElevenLabs SFX always returns MP3
(`audio/mpeg`) regardless of the requested `response_format` — always decode through ffmpeg
before use. There is no duration ceiling in the reader; sub-second and multi-second clips both
work.

## Lessons

1. **The "sync" TTS endpoint is actually async for this model.** `POST /v1/audio/speech`
   returns `{"status": "pending", "runId": ...}` instead of raw audio bytes. Poll
   `GET /tts/status?runId=<id>&model=<fully-qualified-model-id>` every ~3s until
   `status: "completed"` with an `audioUrl`.

2. **The polling model id is NOT the request model id.** Request with the short id
   (`elevenlabs/sound-effects/v2`); the poll response reports the fully-qualified id
   (`fal-ai/elevenlabs/sound-effects/v2`). Polling with the short id 404s. Keep a
   `STATUS_MODEL_ALIASES` map rather than assuming they're the same string.

3. **`response_format: wav` is silently ignored for ElevenLabs-backed models.** They always
   return `audio/mpeg`. Convert with ffmpeg unconditionally; don't branch on content-type.

4. **Always pass `duration_seconds` in the request body.** Without it, the model pads its
   response to ~5s and — critically — sometimes fills that space with several *separate,
   unrelated candidate takes* scattered across the clip (observed: 3 near-identical "damage"
   impacts at ~0.85s/2.2s/3.75s in one clip; ~8 scattered clicks across 0.8s-4.2s for
   "reload" in another) rather than one clean sound near the start. A naive "first loud
   window wins" trim then grabs the wrong/quietest event and the result is unusable
   (0.06s-0.14s of near-silence). Passing `duration_seconds` (request roughly
   2-2.5x the desired trimmed length, min 1.5s) reliably constrains the model to one focused
   take instead of a multi-take grab-bag. This was the actual root-cause fix, not a smarter
   trim algorithm.

5. **ffmpeg's `silenceremove` filter is unreliable for finding the true onset.** It can't
   distinguish "quiet room tone before the real sound" from "the real sound has started,"
   and can trim to a truncated fragment. Do the trim in Python instead: read raw PCM samples,
   compute peak amplitude in ~10ms windows, find the first window over a start threshold
   (~-34dB) as the true onset, then walk forward bridging quiet gaps up to a `tail_gap_s`
   tolerance (tuned per-sound — e.g. wider for two-click reload sounds) until a longer
   silence or a per-sound `max_duration` cap is hit. This is fully inspectable (print the
   envelope) which is what surfaced lesson 4 in the first place.

6. **Verify by inspecting the raw sample envelope, not just file size/duration.** A file can
   be well-formed PCM and still contain the wrong 50ms of near-silence. Print peak amplitude
   per 10-20ms window for each generated file before accepting it.
