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
  `--all` regenerates all 7 SFX (`Shooting`, `Reload`, `Pickup`, `Jump`, `Dash`, `Damage`,
  `ArmorRicochet`);
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

## Background music (looping tracks)

`tools/nanogpt/generate_music.py` generates audition candidates for a background music loop,
one per model, named `Music_TechnoCowboy_<Model>.wav` so the source is obvious at a glance.
`tools/nanogpt/loop_music.py` then makes a chosen candidate actually loop cleanly. Additional
lessons specific to music generation (on top of the SFX lessons above, which still apply --
async polling, MP3-only responses, model-id aliasing):

7. **Every music model wants slightly different request fields.** The unified `/v1/audio/speech`
   endpoint does NOT normalize provider-specific requirements -- discovered by trial and error:
   - ACE-Step family requires a `style_tags` field (400 `"Style tags are required"` without it).
   - ACE-Step 1.5 and MiniMax additionally require a `lyrics` field even for instrumental tracks
     (400 `"Lyrics must be between 10 and 3000 characters"`); pass `"[instrumental]"`.
   - Mureka's `generate-bgm` requires a top-level `prompt` field in addition to `input` (400
     `"... requires prompt"` otherwise).
   - ACE-Step's duration field is named `duration`, not `duration_seconds` (the latter is
     silently ignored, producing a ~5s clip regardless of what was requested).
   Rather than special-case the request body per model, send `input`, `prompt`, `style_tags`,
   `tags`, `lyrics`, `duration_seconds`, AND `duration` on every request -- verified empirically
   that models which don't need a given field simply ignore it (no errors from Lyria/ElevenLabs
   when given ACE-Step-only fields).

8. **Some models have a stricter content/style-mimicry filter than others.** Google Lyria 3 Pro
   rejected a prompt containing "spaghetti-western" + "whistled ... melody" with
   `CONTENT_POLICY_VIOLATION` (likely flagged as mimicking a specific iconic film-score style,
   e.g. Ennio Morricone's whistle motif) even though every other model (ACE-Step, MiniMax,
   Mureka, ElevenLabs) generated the identical prompt without issue. A much shorter prompt
   ("techno cowboy music") and a reworded detailed prompt (swapping "spaghetti-western
   whistled melody" for "harmonica-like synth lead playing a dusty desert tune") both passed.
   When one model in a multi-model batch fails content policy and the others don't, suspect
   wording that names/imitates a specific recognizable artist or work rather than the genre
   itself, and reword generically.

9. **A model backend can be down independent of request correctness.** `ACE-Step-v1.5-Turbo`
   and `ACE-Step-v1.5-Base` returned a persistent `502 "Music generation service error"` on
   every request regardless of body content (2026-09-20) while the older `ACE-Step` /
   `ACE-Step-1.5` ids on the same family worked fine. If a specific model id 502s repeatedly
   with no request-shape explanation, treat it as a provider outage and swap to a sibling model
   id rather than debugging the request further.

10. **Looping requires a real fix, not just picking a "seamless loop" prompt.** AI music output
    reliably has SOME discontinuity between its last sample and its first sample (often because
    the track fades in from silence but doesn't fade back out to silence) -- looping it verbatim
    via `SoundEffectInstance.IsLooped` produces an audible click every repeat. `loop_music.py`
    fixes this with a *declick* crossfade: over the last `--crossfade` seconds, linearly
    interpolate from the tail's own natural trajectory toward exactly matching sample 0 (not
    toward the head's value at the END of the crossfade window -- that was a real bug hit during
    this work: blending toward `head[N-1]` instead of `head[0]` made the seam WORSE because the
    head is still mid-fade-in at that point). The correct construction guarantees
    `output[-1] == output[0]` exactly, eliminating the sample-level jump (measured RMS
    discontinuity: 914 -> 0 on the first real track), while leaving mid-crossfade sample deltas
    no larger than a normal region of the track (i.e. no new artifacts introduced). Keep the
    crossfade short (~0.1-0.2s) -- a long crossfade (tested at 1.5s) reaches back into the
    fade-in ramp and makes the mismatch worse, not better.
