# First Playable Slice QA Checklist

This checklist is intentionally separate from gameplay code and is usable when Unity is unavailable.

## Automated smoke coverage

The deterministic smoke executable in `tests/Cowbania.Core.Tests` covers:

- Projectile launch direction and speed.
- Player health initialization.
- Player invulnerability preventing immediate double damage.
- RoomCatalog-backed stage bounds, solids, spawns, checkpoints, shortcuts, enemies, and pickups.
- Presentation-only stage layering invariants: RoomCatalog remains collision authority, metadata is unchanged, and equivalent runs are deterministic.
- Paused updates preserving gameplay state and timers without advancing simulation.
- Slot 1 remaining selected and idempotent while slots 2-10 remain inactive.
- Typed, single-use pickups: two currency, one health, and one reserve-ammo pickup.
- Reserve ammo remaining independent from the six-round revolver cylinder and reload result.
- Pickup, economy, checkpoint, shortcut, and objective continuity through transitions and death.
- Fresh `GameWorld` instances resetting all runtime-only progress.
- Shortcut return maintaining a coherent hub checkpoint room and position.
- Objective completion requiring interaction within 42 units of the hub shortcut.

Run with `dotnet run --project tests/Cowbania.Core.Tests --no-restore` and
`dotnet run --project tests/Cowbania.Host.Tests --no-restore`. The host suite verifies
reference-only fatal deduplication, aggregate/unobserved-task reporting and observation,
runtime/startup flush durability, playback failure boundaries, and a nonzero fatal subprocess exit.

## Manual playtest

### Input bindings

- [ ] `A/D` moves horizontally; `W/S` do not apply vertical movement.
- [ ] `Space` jumps only while grounded.
- [ ] A jump produces separate `Cowbania.Host.log` request and accepted/rejected outcome entries;
  an accepted jump is followed by its audio decode and playback boundary entries if the sound is
  not already loaded.
- [ ] `Shift` dashes and does not require an unlocked ability.
- [ ] Arrow keys aim independently of movement in all eight directions.
- [ ] `Ctrl` fires; held fire respects the configured delay.
- [ ] `R` reloads and cannot create more than six revolver rounds.
- [ ] `E` interacts with the intended shortcut/checkpoint.
- [ ] `Esc` pauses and resumes gameplay.
- [ ] `1` selects the revolver.
- [ ] Repeated `1` presses do not change cylinder ammo, reload progress, or any other gameplay state.
- [ ] Top-row keys `2` through `0` do not change the selected weapon or gameplay state.
- [ ] Mouse movement and mouse buttons have no gameplay effect.

### Pause

- [ ] A single `Esc` press edge pauses; holding `Esc` does not repeatedly toggle.
- [ ] While paused, player/enemy positions, health, ammo, reload/dash/fire/invulnerability timers, projectiles, pickups, economy, audio triggers, and animation clocks remain unchanged.
- [ ] Gameplay input is ignored while paused; a later `Esc` press edge resumes.

### Revolver

- [ ] A full cylinder contains exactly six rounds.
- [ ] Each accepted shot consumes exactly one round.
- [ ] Firing with an empty cylinder does not spawn a projectile.
- [ ] Reload restores rounds according to the agreed timed-reload rule.
- [ ] Reload cannot be interrupted into duplicate rounds.
- [ ] Collecting reserve ammo does not alter cylinder ammo or the six-round reload result.

### Pickups and economy

- [ ] Hub `(1120,448)` grants exactly one currency.
- [ ] Hub `(1280,448)` grants one health up to three; at full health it remains available.
- [ ] Branch `(760,448)` grants exactly one reserve ammo without changing cylinder ammo.
- [ ] Branch `(1440,448)` grants exactly one currency.
- [ ] Each successfully collected pickup plays the existing pickup sound once and cannot be collected again.
- [ ] The HUD visibly reports currency and reserve ammo remains runtime state only.

### Health and checkpoint

- [ ] Damage reduces health by one hit at a time.
- [ ] Repeated damage during the invulnerability window is ignored.
- [ ] Zero health respawns at the runtime checkpoint rather than creating a save file.
- [ ] Branch checkpoint activation records both branch room and authored checkpoint position.
- [ ] Shortcut return records both hub room and authored hub checkpoint position.
- [ ] Death preserves collected pickups, currency, reserve ammo, shortcut state, and active checkpoint.
- [ ] Leaving and relaunching resets pickups, currency, reserve ammo, shortcut, checkpoint, and completion state.

### Shortcut objective

- [ ] Interacting within 42 units of the branch shortcut unlocks it and returns to the hub-side shortcut.
- [ ] A hub death after shortcut return respawns in the hub at the active hub checkpoint.
- [ ] Interacting in the hub more than 42 units from its shortcut does not complete the slice.
- [ ] Interacting within 42 units of the hub shortcut after unlocking completes the slice.
- [ ] Completion is terminal until relaunch and freezes gameplay simulation.

### Scope exclusions

- [ ] No shop, boss, dialogue/cinematic, save-file, or second-weapon flow is reachable.
- [ ] Reserved ammo collectibles, if visible, do not alter revolver ammunition.
- [ ] The first slice remains one connected mini-area with the shortcut return objective.

## Stage presentation acceptance

- [ ] Hub and branch use distinct palettes and layered skyline/surface passes.
- [ ] Ground and raised platforms align visually with the corresponding `RoomCatalog.Solids` rectangles.
- [ ] Checkpoint, shortcut, and transition landmarks are present without changing interaction coordinates.
- [ ] Re-running the same input sequence produces the same room, actor, projectile, and animation state.
- [ ] `dotnet build Cowbania.sln --no-restore` and the deterministic smoke executable both pass.

The MonoGame host remains presentation-only for this release: it must consume `RoomCatalog`
metadata and must not duplicate or modify collision geometry.

## Runtime failure diagnostics

- [ ] `Cowbania.Host.log` and `Cowbania.Host.startup.log` are created beside the executable.
- [ ] The reported jump crash is documented as narrowed to the native `SFX_Jump.wav` decode
  boundary, not fixed.
- [ ] A deliberate managed host failure records its source, termination state, full exception
  chain, and durable fatal entry before exit; the process still fails rather than resuming.
- [ ] Fatal deduplication suppresses only repeat reports of the same exception object; distinct
  failures cannot be hidden by a hash collision.
- [ ] Unobserved task failures record the flattened aggregate details and are explicitly marked
  observed after logging. Audio logs contain request, decode begin/complete/failure, and playback
  begin/result/failure boundaries without frame-by-frame logging.
