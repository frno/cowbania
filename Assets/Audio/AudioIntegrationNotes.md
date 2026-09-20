# MonoGame Audio Contract

- Runtime implementation: `src/Cowbania.Host/Audio/`.
- `AudioEventBus` maps `AudioEvent` values to `Assets/Audio/SFX_*.wav`, loads on first use, caches
  successful decodes, and disables repeated failures with a silent non-blocking fallback.
- Add/change SFX by updating `AudioEvent`, `AudioEventBus` filename/volume maps,
  `AudioFeedbackRouter`, the WAV asset, and Host tests together.
- `ManagedPcmWav` requires uncompressed 16-bit PCM WAV (mono or stereo).
- `Music_Title.wav` and `Music_Background.wav` are controlled by `MusicPlayer`; they do not route
  through `AudioEventBus`.
- Loop music before shipping:
  `python tools/nanogpt/loop_music.py <in.wav> <out.wav> --crossfade 0.15`.
- Generation details: `docs/pipelines/NANOGPT_AUDIO.md`.