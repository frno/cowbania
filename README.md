# Cowbania

A MonoGame/.NET 10 2D C# desktop keyboard-only metroidvania prototype with a cowboy/outlaw theme.

## Prototype scope

The player is an outlaw escaping through a dangerous interconnected area. The prototype is a side-view pixel-art slice with crisp nearest-neighbor rendering. Placeholder sprites use 16x16 or 32x32 designs and must remain replaceable.

The first slice contains one connected mini-area with a loop, branching routes, and shortcuts. The player can run, jump, and dash from the beginning; dash gates are environmental rather than ability-unlock gates. Prototype completion is reached by unlocking a shortcut and returning to the hub.

Included systems:

- Movement, jump, and dash
- Independent 8-direction keyboard aim
- A revolver with six rounds, timed reload, and held-fire delay; reload restores rounds according to the prototype reload rules
- Bandit and wildlife enemies with patrol, notice, chase, and attack behaviors
- Three-hit health and runtime-only checkpoint respawn
- Smooth camera follow with room bounds
- Currency (HUD counter only), health, and reserved ammo collectibles; reserved ammo is not active for the revolver
- HUD for health, revolver ammo, selected slot 1, currency, pause, and completion
- Placeholder sound effects for shooting, reload, jump, dash, pickups, and damage
- Player animation states: idle, run, jump, fall, shoot, reload, and hurt
- Placeholder animations for other visible animated objects where appropriate

The weapon architecture should anticipate ten future weapon slots, but only slot 1 (the revolver) is active in this prototype.

## Controls

| Input | Action |
| --- | --- |
| `WASD` | Move |
| `Space` | Jump |
| `Shift` | Dash |
| Arrow keys | Aim independently in 8 directions |
| `Ctrl` | Fire; hold to auto-fire with a delay between shots |
| `R` | Reload |
| `E` | Interact |
| `Esc` | Pause |
| `1` | Select revolver |

Mouse input is not supported.

## Project structure

- `src/Cowbania.Core/` is the deterministic, engine-independent gameplay simulation.
- `src/Cowbania.Host/` is the thin MonoGame desktop host and placeholder renderer.
- `tests/Cowbania.Core.Tests/` is a dependency-free deterministic smoke-test executable.
- `Assets/Art/Placeholders/` are copied beside the host executable and loaded as PNGs at runtime; audio remains available for a later Content Pipeline pass.

The host uses `MonoGame.Framework.DesktopGL` 3.8.2.1105. MonoGame is referenced as a NuGet
package so the repository builds from the command line without Unity or an editor install.
Placeholder sprites are rendered with `SamplerState.PointClamp` and scaled from 16x16 to 32–48
pixels without filtering.

## Release 1 animation architecture

`Cowbania.Core.Animation` contains engine-neutral `AnimationClip` and `AnimationClock`
primitives. Clips explicitly select loop or one-shot playback, while clocks advance only from
elapsed seconds and hold the final frame when a one-shot completes. `PresentationStateSelector`
maps gameplay presentation inputs to player idle/run/jump/fall/shoot/reload/hurt/dash states and
also exposes enemy idle and pickup float states. `PlaceholderAnimationCatalog` maps those states
to the existing placeholder PNG frame names.

The MonoGame host caches every placeholder frame once during `LoadContent`, advances one clock
per visible actor (and one for pickups), and draws the selected frame with `PointClamp`. Actor
feet continue to use the gameplay anchor and source origin `(width / 2, 12)`, the muzzle remains
the existing 24-pixel gameplay offset, and horizontal facing still uses sprite flips. This keeps
animation presentation separate from collision geometry and avoids engine/runtime dependencies in
the core library.

## Release 2 stage presentation

The host renderer now builds a room-specific procedural stage layer using palette-based surfaces,
layered skyline silhouettes, and readable landmark silhouettes around checkpoints, shortcuts,
and room transitions. The visuals are driven from the existing `RoomCatalog` bounds and solids, keep
`PointClamp` and camera-space offsets intact, and use only primitive rectangles so the scene stays
performant without allocating textures every frame.

## Coordinate and stage contract

Gameplay uses screen-space coordinates: +X points right and +Y points down. `GameWorld.PlayerPosition`
is the player's feet/contact point, not the sprite centre; jump velocity is negative Y and gravity is
positive Y. Shared immutable hub and branch definitions live in `Cowbania.Core.RoomCatalog`. Their
room bounds, ground, raised platforms, spawns, checkpoint, shortcut, enemy, and pickup anchors are
consumed by both collision and the MonoGame renderer. The host must not duplicate stage coordinates.
Actor sprites are drawn from a source-space origin at the visible bottom-center of the
16x16 placeholders (x=8, y=13), so transparent source padding does not make actors float
above their collision support point. Projectiles originate 24 pixels above the player feet
anchor, then extend along the current aim direction; this keeps the rendered bullet aligned
with the revolver muzzle without changing collision geometry.

## Running

```powershell
dotnet run --project tests/Cowbania.Core.Tests
dotnet run --project tests/Cowbania.Host.Tests
dotnet run --project src/Cowbania.Host
```

The first playable slice has a hub, branch room, checkpoint, multiple patrol enemies, shortcut return,
movement/jump/dash, eight-way keyboard aim, six-shot revolver, timed reload, projectile damage,
three health points, checkpoint respawn, camera follow, and a minimal HUD. Interact with `E` at
the branch checkpoint and shortcut. After taking the branch shortcut back to the hub, press `E`
again within 42 units of the hub-side shortcut to complete the slice.

Pickup collection, currency, reserved ammo, shortcut state, and the active room/position checkpoint
survive death and room transitions for the current run. Relaunching the game starts a fresh run.
Pausing freezes gameplay, timers, sound triggers, and animation clocks. Only top-row `1` selects a
weapon; it idempotently selects the revolver without changing its ammo or reload progress.

## Runtime diagnostics

The host writes `Cowbania.Host.log` beside the executable using a flushed, size-rolling log
(5 MB per file with three backups). It records lifecycle milestones, throttled Update and Draw
heartbeats, slow-frame warnings, pause/room/objective/checkpoint/death transitions, separate jump
request and accepted/rejected outcome entries, and deferred audio decode and playback boundaries.
Current evidence narrows the reported jump crash to the native `SFX_Jump.wav` decode boundary;
these diagnostics do not claim to fix the crash.
Fatal `AppDomain` exceptions and unobserved task exceptions include source, terminating state,
the complete `Exception.ToString()` value, and each flattened aggregate inner exception. Fatal
reports are deduplicated only when the exact same exception object reaches multiple handlers;
separate exception instances are always retained. Runtime and startup sinks are flushed before
rethrow/nonzero exit. Unobserved task exceptions are logged and then explicitly marked observed
to prevent their finalizer-thread escalation; `Game.Run` and AppDomain fatal exceptions are never
swallowed. The earlier `Cowbania.Host.startup.log` remains available beside it so process-entry,
logger-initialization, and fatal-reporting failures are still visible. A logging setup failure is
reported there but does not prevent the game from launching.

## Explicitly out of scope

- Shops
- Bosses
- Dialogue and story cinematics
- Save files
- Additional weapons in the first slice
- Mouse support
