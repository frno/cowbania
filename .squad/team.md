# Squad Team

> cowbania

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| game-director | Game Design & Production Lead | .squad/agents/game-director/charter.md | Active |
| gameplay-systems | Deterministic Gameplay Engineer | .squad/agents/gameplay-systems/charter.md | Active |
| monogame-runtime | MonoGame Runtime Engineer | .squad/agents/monogame-runtime/charter.md | Active |
| runtime-diagnostics | .NET Runtime Diagnostics Specialist | .squad/agents/runtime-diagnostics/charter.md | Active |
| visual-presentation | Pixel Art & Presentation Designer | .squad/agents/visual-presentation/charter.md | Active |
| quality-engineering | Gameplay QA & Automation Engineer | .squad/agents/quality-engineering/charter.md | Active |
| Scribe | Session Logger | .squad/agents/scribe/charter.md | Active |
| Ralph | Work Monitor | .squad/agents/ralph/charter.md | Active |
| Rai | RAI Reviewer | .squad/agents/rai/charter.md | Active |
| Fact Checker | Fact Checker | .squad/agents/fact-checker/charter.md | Active |

## Coding Agent

<!-- copilot-auto-assign: false -->

| Name | Role | Charter | Status |
|------|------|---------|--------|
| @copilot | Coding Agent | — | 🤖 Coding Agent |

### Capabilities

**🟢 Good fit — auto-route when enabled:**
- Bug fixes with clear reproduction steps
- Test coverage (adding missing tests, fixing flaky tests)
- Lint/format fixes and code style cleanup
- Dependency updates and version bumps
- Small isolated features with clear specs
- Boilerplate/scaffolding generation
- Documentation fixes and README updates

**🟡 Needs review — route to @copilot but flag for squad member PR review:**
- Medium features with clear specs and acceptance criteria
- Refactoring with existing test coverage
- API endpoint additions following established patterns
- Migration scripts with well-defined schemas

**🔴 Not suitable — route to squad member instead:**
- Architecture decisions and system design
- Multi-system integration requiring coordination
- Ambiguous requirements needing clarification
- Security-critical changes (auth, encryption, access control)
- Performance-critical paths requiring benchmarking
- Changes requiring cross-team discussion

## Project Context

- **Project:** cowbania
- **User:** Fredrik Norum
- **Goal:** Create a playable cowboy-themed metroidvania proof of concept in C# and MonoGame, with deterministic gameplay, responsive runtime diagnostics, sound, sprite animation, and an SNES/NEO-GEO-inspired presentation.
- **Created:** 2026-09-19
- **Reassessed:** 2026-09-19 for the .NET 10 MonoGame architecture and current release workflow.
