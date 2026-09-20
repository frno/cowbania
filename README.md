# Cowbania

A MonoGame/.NET 10 2D C# desktop keyboard-only metroidvania prototype with a cowboy/outlaw theme.

## Prototype scope

The player is an outlaw escaping through a dangerous interconnected area. The prototype is a side-view pixel-art slice with crisp nearest-neighbor rendering. Release 6 uses the original Frontier visual pack, with 16x16 actor, pickup, terrain, effect, and HUD art plus larger landmark and background assets.

The first slice contains one connected mini-area with a loop, branching routes, and shortcuts. The player can run, jump, and dash from the beginning; dash gates are environmental rather than ability-unlock gates. Prototype completion is reached by unlocking a shortcut and returning to the hub.

Included systems:

- Movement, jump, and dash
- Independent 8-direction keyboard aim
- A revolver with six rounds, timed reload, and held-fire delay; reload restores rounds according to the prototype reload rules
- Bandit and wildlife enemies with patrol, notice, chase, and attack behaviors
- Armadillos deflect bullets while armored but become vulnerable during their post-roll recovery
- Three-hit health and runtime-only checkpoint respawn
- Smooth camera follow with room bounds
- Score coins, health, and reserved ammo collectibles; each coin awards 100 points and reserved ammo is not active for the revolver
- HUD for health, revolver ammo, selected slot 1, centered global score and current-room coin progress, pause, and completion
- Sound effects for shooting, reload, jump, dash, pickups, player damage, and distinct hit/defeat cues for every enemy archetype
- Player animation states: idle, run, jump, fall, shoot, reload, and hurt
- Frontier animations for other visible animated objects where appropriate
- A two-minute looping NanoGPT western title montage, dimmed over black beneath the centered Cowbania logo
- A grand NanoGPT title theme that shares the gameplay soundtrack's techno-cowboy motif and hands off cleanly when play begins

The title ships with cinematic and pixel-art cuts. The pixel-art cut is the default; set
`COWBANIA_TITLE_FILM=cinematic` before launch to compare the original cinematic cut.

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

## Architecture

Dependencies flow one way: `Cowbania.Host` references `Cowbania.Core`; Core never references
MonoGame, filesystem APIs, runtime services, or Host code. No dependency-injection container,
mediator/event framework, generic `Utils` layer, or additional package is used.

`src/Cowbania.Core/` is organized by feature and responsibility:

- `Gameplay/GameWorld.cs` is the public simulation root. `GameWorld.Update` is the single ordered
  deterministic orchestration boundary; mutable simulation data lives in its internal
  `GameWorldState`.
- `Gameplay/Input`, `Player`, `Combat`, `Enemies`, and `Pickups` contain their public contracts and
  concrete internal systems.
- `Gameplay/World` contains room/progression contracts and `RoomCatalog`;
  `Gameplay/World/Geometry` contains geometry and collision queries.
- `Presentation/Animation` contains engine-neutral clips, clocks, and the Frontier catalog.
  `Presentation/Player`, `Enemies`, and `Pickups` contain feature-specific presentation contracts
  and selectors. Presentation state never becomes simulation authority.
- `Diagnostics` contains engine-independent diagnostic formatting.

`RoomCatalog` is the sole authority for room bounds, solids, transitions, spawns, checkpoints,
shortcuts, enemy definitions, and pickups. Core systems and Host rendering consume this shared
catalog rather than duplicating coordinates.

`src/Cowbania.Host/` is the MonoGame composition and runtime layer:

- `Application/Program.cs` installs diagnostics, establishes the executable working directory,
  creates `CowbaniaGame`, runs it, reports fatal failures, and flushes logging on exit.
- `Application/CowbaniaGame.cs` owns the MonoGame lifecycle. Its constructor configures the window
  and lifecycle logging; `LoadContent` composes assets, audio, input, update coordination,
  presentation, camera, and rendering; `Update` delegates one elapsed-time step to
  `GameUpdateCoordinator`; `Draw` renders the current Core state.
- `Input`, `Audio`, `Diagnostics`, and `Presentation` contain concrete host services. Host feedback
  consumes Core snapshots and transitions but cannot mutate or replace gameplay authority.

`tests/Cowbania.Core.Tests/` and `tests/Cowbania.Host.Tests/` are dependency-free executable
regression suites. `Assets/Art/Frontier/` contains the runtime PNG manifest copied beside the host
executable; audio remains file-loaded without a Content Pipeline dependency.

The host uses `MonoGame.Framework.DesktopGL` 3.8.2.1105. MonoGame is referenced as a NuGet package
so the repository builds from the command line without Unity or an editor install. Frontier
sprites are rendered with `SamplerState.PointClamp` at integer scale without filtering.

## Animation architecture

`Cowbania.Core.Presentation.Animation` contains engine-neutral `AnimationClip`, `AnimationClock`,
`PresentationAnimationClock`, and `FrontierAnimationCatalog` types. Clips explicitly select loop or
one-shot playback, while clocks advance only from elapsed seconds and hold the final frame when a
one-shot completes. Feature selectors under `Cowbania.Core.Presentation.Player`, `.Enemies`, and
`.Pickups` map typed simulation snapshots or presentation inputs to animation states.

The MonoGame host caches required Frontier frames during `LoadContent`, advances presentation
clocks for visible actors and pickups, and draws selected frames with `PointClamp`. Actor feet use
authored source anchors, effects use catalogued anchors, and horizontal facing uses sprite flips.

## Release 2 stage presentation

The host renderer now builds a room-specific procedural stage layer using palette-based surfaces,
layered skyline silhouettes, and readable landmark silhouettes around checkpoints, shortcuts,
and room transitions. The visuals are driven from the existing `RoomCatalog` bounds and solids, keep
`PointClamp` and camera-space offsets intact, and use only primitive rectangles so the scene stays
performant without allocating textures every frame.

## Coordinate and stage contract

Gameplay uses screen-space coordinates: +X points right and +Y points down. `GameWorld.PlayerPosition`
is the player's feet/contact point, not the sprite centre; jump velocity is negative Y and gravity is
positive Y. Shared immutable hub and branch definitions live in
`Cowbania.Core.Gameplay.World.RoomCatalog`. Their room bounds, ground, raised platforms, spawns,
checkpoint, shortcut, enemy, and pickup anchors are consumed by both collision and the MonoGame
renderer. The host must not duplicate stage coordinates.
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
three health points, checkpoint respawn, camera follow, and a minimal HUD. Reach the tall trail bell at
the far right of Dustwind Crossing and touch its hanging rope to enter Rattlesnake Run. Interact with `E` at the branch checkpoint
and shortcut. After taking the branch shortcut back to the hub, press `E`
again within 42 units of the hub-side shortcut to complete the slice.

Pickup collection, score, coin progress, reserved ammo, shortcut state, and the active room/position checkpoint
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
reported there but does not prevent the game from launching. Automated coverage exercises fatal
formatting, deduplication, aggregate expansion, observation, and sink durability through in-process
test seams; routine tests and launches do not deliberately terminate the host.

## Release 5 enemy encounters and certification status

Release 5 turns the existing enemy placements into a small, readable combat encounter pass without
adding rooms, weapons, bosses, saves, or new content-pipeline dependencies. The existing four spawn
slots are assigned between bandit and wildlife archetypes. Both use deterministic
patrol/notice/chase/attack/defeated state, while bandits provide a telegraphed ranged threat and
wildlife provide a telegraphed close-range lunge. Enemies remain constrained to their authored room
and leash, and the current damage, invulnerability, checkpoint, shortcut, pause, and completion
contracts remain unchanged.

The gameplay-stall instrumentation and fatal exception coverage are complete and passing. The
reported jump failure itself is only narrowed to native `SFX_Jump.wav` decoding, not fixed. Enemy
work may proceed immediately, but Release 5 cannot be certified until a manual packaged-build run
either completes the keyboard-to-shortcut loop with jump audio enabled or records a durable terminal
audio boundary and ships a verified non-stalling fallback.

## Release 6 Frontier Visual Identity

Release 6 replaces runtime placeholder art and primitive-only identity cues with the original
`Assets/Art/Frontier` pack while preserving the Release 5 gameplay, deterministic snapshot, pause,
completion, collision, audio-fallback, and certification contracts. It covers player, bandit,
wildlife, pickups, terrain, props, backgrounds, effects, and HUD icons. Rendering remains
presentation-only, consumes `GameWorld`/`RoomCatalog` state, uses `PointClamp`, integer scaling, and
an explicit back-to-front depth order. Missing required art is a startup error rather than a silent
fallback to rectangles or placeholder files.

The visual hierarchy is a release rule: foreground and jumpable surfaces must be immediately
distinct from scenery through **value, contrast, edge, thickness, silhouette, and material
treatment**. Decorative scenery must not create false platform edges. Hazards, pickups,
interactables, enemy archetypes, and attack telegraphs cannot rely on color alone; shape, pose,
outline, motion, iconography, or another non-color cue must remain readable in grayscale and common
color-deficiency simulations.

Release 5 automated implementation remains complete, but its interactive packaged Windows
certification remains required. Release 6 does not retroactively mark that gate complete.

## Explicitly out of scope

- Shops
- Bosses
- Dialogue and story cinematics
- Save files
- Additional weapons in the first slice
- Mouse support
