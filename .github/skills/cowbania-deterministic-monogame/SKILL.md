---
name: "cowbania-deterministic-monogame"
description: "Use when implementing or refactoring Cowbania.Core simulation behavior or its MonoGame Host integration. Do not load for repository administration, documentation-only work, or test-harness-only maintenance."
domain: "gameplay"
confidence: "high"
source: "project-architecture"
---

## Purpose

Preserve Cowbania's deterministic simulation/presentation boundary during normal implementation work.

## Architecture guardrails

- Dependencies flow one way: `Cowbania.Host` references `Cowbania.Core`; Core remains free of MonoGame, filesystem, and runtime-service dependencies.
- `Cowbania.Core.Gameplay.GameWorld.Update` is the single ordered deterministic orchestration boundary. Authoritative mutable simulation state stays in `GameWorld` or its internal state.
- Core is feature-oriented under `Gameplay`, `Presentation`, and `Diagnostics`; keep contracts and concrete systems in their owning feature namespace.
- `CowbaniaGame` owns MonoGame lifecycle and composition. Host input, audio, diagnostics, presentation, camera, and rendering remain concrete runtime services.
- `RoomCatalog` is the sole authority for geometry, transitions, spawns, checkpoints, shortcuts, enemies, and pickups.
- Presentation consumes snapshots and catalog metadata; it must never mutate or duplicate simulation geometry or infer authoritative gameplay state.
- Do not introduce generic `Common`/`Helpers`/`Utils`, dependency injection, mediator/event frameworks, interfaces without substitution, or new dependencies.

See [README.md](../../../README.md#architecture) for the current folder and namespace map.

## Change rules

1. Advance simulation exclusively from explicit input and elapsed time. Equivalent initial state, inputs, and time steps must produce equivalent results.
2. Pause and objective completion freeze both simulation and presentation clocks, including actors, projectiles, effects, audio triggers, and animation.
3. Preserve authored feet and effect anchors. Render pixel art with `SamplerState.PointClamp` and integer scaling.
4. Keep gameplay surfaces, hazards, pickups, interactables, enemy archetypes, and telegraphs readable through non-color cues such as silhouette, pose, outline, motion, iconography, edge, or value.
5. Treat audio as feedback only. Decode/playback failures must emit terminal diagnostics, disable repeated failing work where applicable, and fall back without stalling simulation.
6. Behavior or public API changes require matching custom executable tests in the same change.
7. Prefer snapshots/catalog data over host-side caches; caches may retain presentation resources or clocks, never duplicate game truth.

## Validation

Run exactly from the repository root:

```powershell
dotnet run --project tests\Cowbania.Core.Tests --no-restore
dotnet run --project tests\Cowbania.Host.Tests --no-restore
dotnet build Cowbania.sln --no-restore
```

Use [README.md](../../../README.md) for runtime context and
[QA/FirstPlayableSliceChecklist.md](../../../QA/FirstPlayableSliceChecklist.md) for detailed
automated and manual acceptance criteria.
