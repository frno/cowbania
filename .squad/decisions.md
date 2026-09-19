# Squad Decisions

## Active Decisions

### 2026-09-19T10:27:50.931+02:00: Unity cowboy metroidvania planning manifest
**By:** Lead, Marshal

**What:**
- Lead produced a dependency-aware implementation roadmap for the original Unity cowboy metroidvania prototype.
- Marshal produced QA acceptance checks and identified ambiguities around dash, reload, respawn persistence, neutral aim, pause, and completion.

**Why:**
- The team needs a shared planning baseline before implementation and playtest work.
- The QA list captures acceptance conditions and unresolved edge cases for timely clarification.

### 2026-09-19T11:09:35.831+02:00: Use MonoGame as the project engine
**By:** Fredrik Norum (via Copilot)
**What:** Use MonoGame instead of Unity for the cowboy metroidvania proof of concept.
**Why:** User direction.

### 2026-09-19: Recommend MonoGame for a code-first engine migration
**By:** Lead
**What:** Recommend replacing Unity with MonoGame on a .NET 8 Windows desktop project, with pure C# gameplay in a testable core library and a thin MonoGame host.
**Why:** The stated priority is `dotnet build`/run without Unity Editor serialization. MonoGame best satisfies that requirement for this small 2D prototype; Godot C# is the best editor-driven alternative but still relies on Godot scenes/resources, while Stride is higher-risk for a 2D-first project.

### 2026-09-19: MonoGame smallest-slice architecture
**By:** Lead
**What:** Build the migration as a .NET 8 MonoGame desktop project with a pure C# gameplay core and a thin MonoGame host. The first playable slice should be one authored mini-area containing movement, jump, dash, eight-direction keyboard aim, revolver fire/reload, one patrol enemy, damage/respawn, one checkpoint/shortcut interaction, camera follow, HUD, and placeholder audio.
**Why:** Unity scenes, prefabs, physics components, Animator assets, and ScriptableObjects are absent or engine-specific in this repository. A code-first core preserves the prototype's behavior while making the slice buildable and testable without the Unity Editor; the existing PNG/WAV placeholders remain useful through the MonoGame Content Pipeline.

### 2026-09-19: Player animation integration contract
**By:** Lead
**What:** The player Animator uses integer `State` values: 0 idle, 1 run, 2 jump, 3 fall, 4 shoot, 5 reload, and 6 hurt. `AnimatorStateDriver` owns transient shoot/hurt requests and exposes `IsTransient`; locomotion updates must not overwrite the Animator while that flag is active. Gunslinger owns component and scene/prefab wiring; Pixelwright owns clips/controller/assets; Saloon consumes existing shoot and damage audio events without coupling to animation timing; Marshal verifies the mapping and transient behavior.
**Why:** This preserves the existing animation fix while keeping gameplay, art, audio, and QA responsibilities separate. The current repository has no Unity scene or prefab, so wiring remains an explicit integration blocker for the first playable slice.

### 2026-09-19: MonoGame host targets .NET 10
**By:** Gunslinger
**What:** Replaced the Unity runtime project with a .NET 10 solution containing a pure C# gameplay core, a MonoGame DesktopGL host, and deterministic smoke tests. The host references MonoGame.Framework.DesktopGL 3.8.2.1105 from NuGet.
**Why:** This preserves the requested first playable behavior while making gameplay testable and buildable without Unity serialization or editor tooling. Existing PNG/WAV assets remain reusable for a future Content Pipeline pass.

### 2026-09-19T11:21:02.783+02:00: Sprite art and elapsed-time fix wave
**By:** Gunslinger, Pixelwright

**What:**
- Sprite assets now resolve from copied PNG files and render with `SamplerState.PointClamp` for crisp pixel-art presentation.
- The placeholder colored rectangles for actors were replaced by sprite textures for the player, enemy, and pickups.
- The gameplay constants were corrected from frame-based values to elapsed-time simulation so movement, gravity, dash timing, and projectile speed match the `Update(InputFrame input, float elapsedSeconds)` contract.

**Why:**
- The MonoGame prototype needs readable placeholder art and consistent motion under the time-based update loop.
- Pixel-art rendering requires nearest-neighbor sampling, and gameplay timing must use seconds instead of frames to stay deterministic and stable.

**Validation:**
- `dotnet build Cowbania.sln --no-restore` succeeded with 0 warnings and 0 errors.
- `dotnet test Cowbania.sln --no-restore --nologo --verbosity minimal` exited 0.

### 2026-09-19T11:28:12.016+02:00: Approved MonoGame stabilization plan and implementation wave
**By:** Fredrik Norum (approval), Gunslinger, Pixelwright, Marshal, Scribe

**What:**
- Treat the current .NET 10 MonoGame solution, pure gameplay core, thin DesktopGL host, copied sprite assets, PointClamp rendering, and elapsed-time simulation as the approved stabilization baseline.
- Record the sprite-art and timing fixes as the completed implementation wave; no engine rollback or Unity restoration is planned.
- Continue from this baseline with regression and first-slice QA validation, preserving the passing build and test commands as the required stabilization gate.

**Why:**
- The migration and fix wave now provide a buildable, deterministic, readable prototype without Unity Editor dependencies.
- Explicitly locking the baseline prevents parallel work from reintroducing frame-based timing, rectangle placeholders, or engine-specific assumptions while QA and remaining slice work proceed.

**Validation:**
- `dotnet build Cowbania.sln --no-restore` previously succeeded with 0 warnings and 0 errors.
- `dotnet test Cowbania.sln --no-restore --nologo --verbosity minimal` previously exited 0.

### 2026-09-19T11:46:31.374+02:00: Release 1 animation kickoff
**By:** Scribe
**What:**
- Wire the existing placeholder PNG animation frames into a reusable MonoGame animation runtime.
- Add state-driven playback for player, enemy, and pickup actors using the current placeholder sprite data.
- Cache frame loads and sprite metadata so animation instances can reuse loaded content safely.
- Preserve each frame's anchor/offset values so transformed sprites remain aligned with world positions and hitboxes.
- Add deterministic tests covering playback states, cache behavior, and anchor preservation.
**Why:**
- The MonoGame baseline is stable and the upcoming release gate is visual-readiness rather than engine rework.
- The repository already contains placeholder PNG animation frames, so the lowest-risk Release 1 path is to integrate them into a reusable runtime rather than invent a new art pipeline.
- Cached loading plus deterministic tests make animation playback reliable, reviewable, and stable under the time-based update loop.

### 2026-09-19T11:44:53.728+02:00: Approved visual-production planning wave
**By:** Fredrik Norum (approval), Pixelwright, Gunslinger, Marshal, Scribe
**What:**
- The approved next wave moves the MonoGame host beyond static placeholders toward presentation-ready visuals.
- Scope includes animation playback, sprite sheets/atlas metadata, stage tiles/backgrounds/props, VFX, camera framing, HUD readability, asset validation, performance limits, and visual acceptance gates.
- Scribe records this as planning only and does not modify product code.
**Why:** The MonoGame baseline is stabilized, while explicit art-production scope is needed to make visual work visible, testable, and reviewable.

### 2026-09-19: Release 2 room stage presentation slice
**By:** Pixelwright
**What:** Kept RoomCatalog as the sole source for room bounds, surfaces, landmarks, and placements while expanding the MonoGame renderer with explicit layered passes: room-specific backdrop dressing, tile-like surface fills, room props, and a terrain foreground pass.
**Why:** The stage now reads as two distinct authored spaces without changing collision geometry or gameplay metadata. Rendering remains camera-relative and deterministic, with props keyed only by existing room IDs.

### 2026-09-19: Align actor and projectile camera-space rendering
**By:** Gunslinger
**What:** Reused a shared world-to-camera point conversion for projectile and actor rendering, and changed the actor sprite source origin to `(width / 2, 13)` so the visible placeholder feet align with the gameplay feet anchor.
**Why:** The Release 2 renderer must consume `RoomCatalog` coordinates without introducing presentation-only offsets. The documented source-space origin is y=13; using y=12 made actors render three pixels above their collision support after 3x scaling.
**Validation:** `dotnet build Cowbania.sln --no-restore` and `dotnet run --project tests/Cowbania.Core.Tests --no-build` passed.

### 2026-09-19: Add Release 2 stage-layering regression gates
**By:** Marshal
**What:** Added deterministic acceptance checks for RoomCatalog metadata preservation, collision-authority stability after simulation, and equivalent-run determinism. Updated the first-slice QA checklist with MonoGame stage-presentation acceptance criteria and the current smoke command.
**Why:** Release 2 is presentation-only; these checks make it harder for renderer changes to duplicate or mutate room geometry or disturb the established gameplay/build baseline.

### 2026-09-19: Keep host window explicitly windowed and titled
**By:** Gunslinger
**What:** The MonoGame host now explicitly sets a `Cowbania` window title and disables fullscreen hardware switching while retaining the existing 1024x576 window.
**Why:** The built executable and copied assets/runtime dependencies are present and the process remains alive when launched. A visible window requires an interactive Windows desktop session with a usable OpenGL/SDL display; non-interactive process checks can confirm liveness but cannot display the SDL window. Explicit windowed/title settings make the intended presentation unambiguous without changing gameplay.

### 2026-09-19: Explorer-launch diagnostics and QA gate
**By:** Gunslinger, Marshal
**What:** Program startup now sets the working directory to `AppContext.BaseDirectory`, catches startup exceptions, logs them beside the executable, and shows a Windows MessageBox. Marshal accepts this as a Release 3 QA gate: startup is independent of caller working directory, while manual packaged-build verification remains required for visible window, input, asset loading, and intentional missing-asset diagnostics.
**Why:** This closes the distribution risk exposed by launching from Explorer or a shortcut. Build/tests passed; the five-second process-survival probe produced no startup log, while full window/input verification was environment-limited because no desktop/display was available.

### 2026-09-19T12:07:20.570+02:00: Release 3 audio-feedback slice and QA audit
**By:** Saloon, Marshal, Scribe

**What:**
- `AudioEventBus` and the MonoGame host integration now load the six existing WAV assets from `Assets/Audio`.
- Sound events cover shooting, reload, jump, dash, and damage.
- The deterministic core and build validation passed without introducing gameplay drift or host-only state mutations.
- Marshal identified the next priority as first-playable loop/state coverage across pause/objective continuity, weapon select contract, pickup/economy behavior, and host-level end-to-end flow.

**Why:**
- Release 3 is the first feedback pass that makes the prototype feel responsive without changing the approved gameplay core or room metadata.
- The remaining risk is no longer build instability; it is the completeness of the play loop and state continuity the README claims to support.

**Validation:**
- `dotnet build Cowbania.sln --no-restore` passed.
- `dotnet test Cowbania.sln --no-restore --nologo --verbosity minimal` passed.

### 2026-09-19: Release 3 first-playable gap audit
**By:** Marshal
**What:** Treat Release 3 as a play-loop certification pass: lock rules for interaction, objective completion, pause state, and pickup/economy behavior, then add targeted regression checks before adding more systems.
**Why:** The visual baseline and deterministic smoke suite are stable, but current tests and code do not verify the README's claimed pause, objective, pickup, and weapon-select loop end to end.

### 2026-09-19T12:22:09.216+02:00: Release 2 layered stage visuals orchestration wave
**By:** Pixelwright, Gunslinger, Marshal, Scribe
**What:**
- Pixelwright implemented the Release 2 layered stage visuals.
- Gunslinger integrated render layers and host plumbing, including Explorer-launch diagnostics.
- Marshal validated deterministic presentation, regression behavior, and accepted the Explorer-launch diagnostics as a QA gate.
- The approved .NET 10 MonoGame prototype remains the shared baseline, with RoomCatalog authoritative for collision and room metadata.
- Scribe logged the wave and did not modify product code.
**Why:** This wave advances presentation while preserving deterministic gameplay, room metadata authority, and distribution diagnostics.
**Validation:** Build/tests passed for the Explorer-launch fix; process survival was verified for five seconds without a startup log, while visible-window verification remained environment-limited due to the unavailable desktop/display.


### 2026-09-19T17:10:01.800+02:00: Release 4 first-playable loop implementation and certification (consolidated)
**By:** Lead, Gunslinger, Marshal

**What:**
- Release 4 closes the first-playable loop without adding rooms, weapons, enemies, save data, or art-pipeline scope.
- The deterministic core owns pause continuity, slot-1 revolver selection, typed single-use pickups, run-scoped currency and reserve ammo, checkpoint room/position, shortcut continuity, and terminal objective completion. The host maps top-row `1`, freezes presentation clocks with simulation state, emits audio from core transitions, and renders primitive HUD feedback.
- Pickup rules preserve health pickups at full health, keep reserve ammo independent of the six-round cylinder, and persist collected pickups/economy/checkpoint/shortcut state through room transitions and death for the current process only.
- Objective completion requires interaction within the normal 42-unit radius at the hub shortcut after the branch shortcut loop; completion freezes simulation until relaunch.
- Certification covers full paused-world snapshots, private timers, idempotent slot selection, unavailable slots 2–10, pickup/economy behavior, death continuity, fresh-world reset, checkpoint coherence, objective boundaries, and ordinary grounded pickup contact.
- Marshal initially rejected the integration because exact-radius grounded contact at the authored pickup anchors was excluded. Under reviewer lockout, Lead independently changed the boundary behavior and added deterministic grounded-contact regression coverage. Marshal then approved the revised Release 4 implementation.

**Why:**
- The stable build, presentation, startup, and audio baseline still lacked a fully verified keyboard-to-completion flow and coherent run-state continuity.
- Keeping authoritative state in the deterministic core prevents superficial host-only behavior and makes pause, death, transition, pickup, and completion rules regression-testable.
- The exact-radius revision ensures visible ordinary player-body contact collects pickups rather than requiring an unintended jump.

**Validation:**
- `dotnet build Cowbania.sln --no-restore` passed.
- The deterministic smoke suite passed, including the grounded exact-radius pickup regression.
- Marshal final verdict: APPROVE.

### 2026-09-19: Host stall-diagnostics logging contract
**By:** Gunslinger

**What:** The MonoGame host uses log4net 3.4.0 to write an immediate-flush, 5 MB size-rolling `Cowbania.Host.log` beside the executable with three backups. Two-second Update/Draw heartbeats, 100 ms game-frame or 50 ms execution warnings, lifecycle and gameplay-state transitions, deferred audio load timings/failures, and unhandled exceptions are logged. The original `Cowbania.Host.startup.log` remains independent and bridges later milestones into log4net.

**Why:** A stall after several seconds needs a durable last-known lifecycle location without per-frame noise. Independent early startup output ensures a log4net initialization failure remains explicit and non-fatal.

**Validation:** Build and deterministic smoke validation passed. A seven-second host probe stayed alive and logged heartbeats through 6.8 seconds; Marshal approved the logging integration.