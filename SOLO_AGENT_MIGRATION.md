# Solo-Agent Migration Plan

This checklist is the durable plan for making Cowbania efficient and unambiguous for solo-agent
work. Keep it until every item is complete. Check an item only after its acceptance condition is met.

## P0 — Remove costly orchestration and engine ambiguity

- [x] Remove the Squad coordinator, roster, histories, templates, routing, memory, and orchestration
  state (`.squad/` and `.github/agents/squad.agent.md`).
- [x] Remove Squad's MCP bootstrap and Copilot/Cline session defaults (`.mcp.json`, `.copilot/`, and
  the VS Code `Squad` default mode).
- [x] Remove Squad-specific GitHub workflows so they cannot spend CI minutes, mutate labels, triage
  issues, or create release branches.
- [x] Replace the Squad instruction chain with a short solo-agent guide (`AGENTS.md`) and a minimal
  Cline entry point (`.clinerules/01-project.md`).
- [x] Remove Unity-only animation controllers, metadata, scripts/config, and empty Unity project
  directories while retaining the PNG/WAV/video files consumed by MonoGame.
- [x] State explicitly in agent guidance and README that `Assets/` is a MonoGame runtime asset root,
  not evidence that this is a Unity project.

## P1 — Make the fast path reliable

- [x] Add one real CI workflow that restores, builds, and runs both executable test suites.
- [x] Put the canonical local build/test sequence in `AGENTS.md`.
- [x] Preserve specialized NanoGPT knowledge as opt-in docs instead of auto-discovered Squad skills.
- [ ] Run one manual desktop smoke test on Windows after the current unrelated gameplay work is
  stable. Acceptance: launch the Host and exercise movement, combat, a room transition, pause, and
  audio without a stall; record the result in the relevant QA checklist.

## P2 — Reduce future context and navigation cost

- [ ] Replace release-by-release narrative in `README.md` with a short current-state description and
  move historical release notes to `docs/history/`. Acceptance: README remains sufficient to build,
  run, and understand the current architecture without obsolete implementation history.
- [ ] Split `QA/FirstPlayableSliceChecklist.md` into a short current smoke checklist plus archived
  release certification notes. Acceptance: an agent can identify current required checks without
  reading old release gates.
- [ ] Add a small asset-validation script or test that rejects Unity-only extensions (`.meta`,
  `.anim`, `.controller`, `.unity`, `.prefab`, `.asset`) and verifies required runtime assets.
  Acceptance: accidental Unity residue or missing shipped assets fails locally and in CI.
- [ ] Review this plan after the next two substantial solo-agent tasks. Remove rules that did not
  prevent real mistakes and add only recurring, evidenced failure modes; avoid growing a prompt
  handbook.

## Deliberately not planned

- A new multi-agent framework, role/persona documents, agent memory logs, or automated AI triage.
- Broad production-code refactors solely for agent convenience.
- New dependencies, test frameworks, or documentation generators without a product need.
- Renaming `Assets/`: the Host project already references it explicitly, and the engine declaration
  plus removal of Unity artifacts resolves the ambiguity at much lower risk.