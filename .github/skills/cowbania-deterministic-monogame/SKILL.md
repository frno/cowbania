---
name: "cowbania-deterministic-monogame"
description: "Use for normal Cowbania gameplay/Core or Host implementation, including input, collision, rooms, animation, rendering, assets, audio, diagnostics, and matching behavior/API tests. This is the general Cowbania coding skill; do not load it for repository administration or test-harness-only maintenance."
domain: "gameplay"
confidence: "high"
source: "project-architecture"
---

## Purpose

Preserve Cowbania's deterministic simulation/presentation boundary during normal implementation work.

## Ownership boundaries

- `Cowbania.Core` owns deterministic, engine-independent simulation. Keep gameplay state, rules, timers, collision, transitions, and snapshot production free of MonoGame/runtime dependencies.
- `Cowbania.Host` owns MonoGame input mapping, snapshot-driven rendering, assets, audio, and diagnostics. Host feedback must not become simulation authority.
- `RoomCatalog` is the sole authority for room bounds, solids, transitions, spawns, checkpoints, shortcuts, enemies, and pickups.
- Presentation consumes snapshots and catalog metadata; it must never mutate or duplicate simulation geometry or infer authoritative gameplay state.

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

Use [README.md](../../../README.md) for architecture and runtime context and [QA/FirstPlayableSliceChecklist.md](../../../QA/FirstPlayableSliceChecklist.md) for detailed automated and manual acceptance criteria.
