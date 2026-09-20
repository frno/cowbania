# Audio integration points

`AudioEventBus` is the centralized placeholder audio surface. Add one
`AudioEventBus` component to a scene object and assign
`PlaceholderAudioConfig.asset`.

Gameplay can trigger replaceable sounds without owning clips:

```csharp
AudioEventBus.Raise(AudioEvent.Shooting);
```

Use the event at the action boundary:

- Shooting: after a shot is accepted by `GunController.Fire`.
- Reload: when reload starts or completes.
- Jump: after `PlayerController2D` applies jump velocity.
- Dash: when dash movement is accepted.
- Pickup: after a pickup is collected.
- Damage: after `PlayerHealth.TakeDamage` accepts damage.

`GunController`, `PlayerController2D`, and `PlayerHealth` now raise the
shooting, reload, jump, dash, and damage events at their action boundaries.
Pickup remains available for future pickup scripts; no pickup gameplay script
exists yet.

There is currently no scene in the project. Before audio can be heard, create
one scene-level object with an `AudioEventBus` component (which requires an
`AudioSource`) and assign `PlaceholderAudioConfig.asset` to its config field.
The bus owns playback; other systems may subscribe to
`AudioEventBus.EventRaised` when they need telemetry, UI feedback, or an
alternate mixer route.

## Background music

`Music_Background.wav` is a looping background track, played independently
of the SFX above by `Cowbania.Host.Audio.MusicPlayer` (started once from
`CowbaniaGame.LoadContent`, not routed through `AudioEventBus`). It must be
pre-processed for seamless looping before being dropped in this folder --
MonoGame's `SoundEffectInstance.IsLooped` just seeks back to sample 0 the
instant playback reaches the end, so any leftover discontinuity between the
last and first sample is an audible click on every loop. Use
`tools/nanogpt/loop_music.py <in.wav> <out.wav> --crossfade 0.15` to smooth
the seam (see `.github/skills/nanogpt-audio-pipeline/SKILL.md` for the
generation + looping pipeline and lessons learned).
