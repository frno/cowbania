# Cowbania Agent Rules

## Project

- **C# / .NET 10 / MonoGame DesktopGL. Not Unity.**
- `Cowbania.sln` is the build root. `Assets/` contains files copied directly by
  `src/Cowbania.Host/Cowbania.Host.csproj`; there are no Unity scenes, prefabs, Animator files, or
  Content Pipeline.
- Current scope: two levels, one active weapon (revolver), several enemy types. More are planned;
  extend catalogs/contracts instead of scattering fixed-count assumptions.
- Work as one agent. Do not create teams, personas, handoffs, orchestration logs, or subagent tasks.

## Workflow

1. Read `git status --short`; preserve unrelated changes.
2. Read only relevant code/tests and the applicable reference below. Search before broad reads.
3. Make the smallest coherent change; do not repeatedly re-plan established scope.
4. Test the affected layer and report changes, commands, and unperformed manual checks.

Do not preload all of `docs/`, `QA/`, or asset manifests.

## Decision discipline

- Separate the requested outcome from any suggested implementation. Check the implementation against
  existing contracts, architecture, and project conventions before accepting it.
- If the suggested approach is brittle, inconsistent, or likely to produce the wrong behavior, say
  so before editing. Recommend the better approach and briefly state the tradeoff; do not silently
  implement a known-poor mechanism.
- Ask one focused question only when repository evidence cannot resolve an uncertainty and different
  answers would materially change behavior, public contracts, destructive work, or paid generation.
- For low-risk, reversible ambiguity, follow the nearest existing convention, state the assumption,
  and proceed. Do not turn routine work into an interview.
- Do not invent product rules, constants, or acceptance criteria. Derive them from existing contracts
  or confirm them. Once the user decides, implement without reopening the same decision.

## Architecture invariants

- `Cowbania.Host` depends on `Cowbania.Core`; Core must not depend on MonoGame, Host, filesystem, or
  runtime services.
- `GameWorld.Update` is the deterministic simulation boundary. `RoomCatalog` owns room geometry,
  transitions, spawns, checkpoints, shortcuts, enemies, and pickups.
- Host owns lifecycle, input, rendering, audio, camera, and diagnostics. Presentation reads
  snapshots/catalog data; it does not mutate or duplicate simulation truth.
- Pause and objective completion freeze simulation and presentation clocks.
- Preserve sprite anchors, integer scaling, `SamplerState.PointClamp`, and non-color readability.
- Audio failure must log a terminal boundary, disable repeated failing work, and fall back without
  stalling simulation.
- Avoid speculative abstractions, generic utility layers, DI/mediator frameworks, interfaces without
  substitution, and unrequested dependencies.

## Validation

Tests are dependency-free executables, not xUnit/NUnit. Register cases through the existing `Run(...)`
inventory and synchronize count assertions.

```powershell
dotnet build Cowbania.sln
dotnet run --project tests/Cowbania.Core.Tests --no-build
dotnet run --project tests/Cowbania.Host.Tests --no-build
```

Run both suites for gameplay contracts or cross-layer changes. Do not claim visual/audio validation
without the relevant manual check.

## Load only when relevant

- Runtime/product: `README.md`
- Manual/release QA: `QA/FirstPlayableSliceChecklist.md`
- Sprite generation: `docs/pipelines/NANOGPT_SPRITES.md`
- Audio generation: `docs/pipelines/NANOGPT_AUDIO.md`

Never request, print, or commit secrets. NanoGPT auth is `NANOGPT_API_KEY` or the gitignored
`tools/nanogpt/.secret/api_key.txt`. Keep generation intermediates in `tools/nanogpt/out/`; after
validation, delete only scratch owned by the completed task. Never delete another task's output,
`.secret/`, scripts, manifests, or approved files under `Assets/`.