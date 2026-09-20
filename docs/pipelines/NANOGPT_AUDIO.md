# NanoGPT Audio

Read only for NanoGPT sound/music work.

## Commands and contracts

- SFX: `python tools/nanogpt/generate_sfx.py --name Shooting --out-dir tools/nanogpt/out/<task>`
- All SFX: replace `--name Shooting` with `--all`.
- Music candidates: `python tools/nanogpt/generate_music.py --all --out-dir tools/nanogpt/out/<task>`
- Title music: `python tools/nanogpt/generate_title_music.py`
- Normalize: `python tools/nanogpt/normalize_sfx.py --target-dbfs -3 <wav...>`
- Loop/declick: `python tools/nanogpt/loop_music.py <in.wav> <out.wav> --crossfade 0.15`
- Scripts require ffmpeg; use `--ffmpeg <absolute-path>` when it is absent from `PATH`.
- `ManagedPcmWav` accepts RIFF/WAVE, uncompressed 16-bit PCM, mono or stereo. Final runtime files go
  in `Assets/Audio/` under names mapped by `src/Cowbania.Host/Audio/AudioEventBus.cs` or
  `MusicPlayer.cs`.

## NanoGPT/API rules

- Auth: `NANOGPT_API_KEY` or `tools/nanogpt/.secret/api_key.txt`. Never put keys in prompts, CLI
  arguments, logs, or tracked files.
- ElevenLabs SFX uses `elevenlabs/sound-effects/v2`. Submission may return `pending`; poll the status
  endpoint with the fully qualified alias `fal-ai/elevenlabs/sound-effects/v2` until `completed`.
- ElevenLabs returns MP3 even when WAV is requested; always decode with ffmpeg.
- Keep `duration_seconds`; the script requests roughly 2.5x the final cap (minimum 1.5 s) to avoid
  padded multi-take output. Use the script's PCM envelope trim, not ffmpeg `silenceremove`.
- Music request fields differ by backend. Keep the compatibility fields implemented in
  `generate_music.py`; do not simplify them without testing all configured providers.
- On content-policy rejection, remove artist/work imitation language and describe instrumentation or
  genre generically. Repeated unexplained 502s from one model usually indicate a provider outage;
  try a configured sibling instead of repeatedly spending calls.
- A prompt saying “seamless” is insufficient. Run `loop_music.py`; keep crossfade near 0.1–0.2 s.

## Acceptance

1. Audition the selected file; verify correct event, no truncation/multiple takes, useful loudness,
   and a clean loop where applicable.
2. Copy only the approved final WAV to `Assets/Audio/`.
3. Run:
   ```powershell
   dotnet build Cowbania.sln --no-restore
   dotnet run --project tests/Cowbania.Host.Tests --no-build
   ```
4. Delete this task's raw MP3s, decoded WAVs, rejected candidates, and comparisons. If interrupted or
   awaiting review, retain them and report exact paths. Never wipe all of `tools/nanogpt/out/`,
   `.secret/`, or runtime assets.