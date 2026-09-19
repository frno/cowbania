---
name: "test-gap-audit"
description: "Use only when explicitly asked to audit coverage gaps, missing tests, regression protection, or whether tests prove a Cowbania behavior; never for ordinary implementation or test execution."
domain: "quality"
confidence: "high"
license: "MIT"
source: "Adapted from github/awesome-copilot test-gap-audit at commit 4f4796f0bf30e105700f97ed8408c12b6aa95e06"
---

# Test Gap Audit

Perform a read-only, evidence-based audit of Cowbania's executable tests. If the
request names a feature, change, or subsystem, stay within that scope and its
direct contracts. Otherwise inventory the repository, prioritize gameplay,
host/runtime boundaries, assets, diagnostics, and release-critical flows.

## Method

1. Inspect `git status --short`, relevant production code, nearby tests, and QA
   checklists.
2. Map important behavior: happy paths, boundaries, failure paths, state
   transitions, deterministic equivalence, pause/death/reset continuity, and
   Core-to-Host contracts.
3. Read the actual `Run("...", () => ...)` cases in
   `tests\Cowbania.Core.Tests\Program.cs` and
   `tests\Cowbania.Host.Tests\Program.cs`.
4. Manually trace setup, action, and assertions. A matching test name, file,
   symbol, or coverage percentage is not proof.
5. Classify each gap:
   - **P1:** release-blocking gameplay, data/state continuity, fatal diagnostics,
     packaged-host contract, or public API behavior.
   - **P2:** meaningful edge case, error path, state transition, or regression.
   - **P3:** lower-risk clarity, fixture, or assertion improvement.
6. Recommend the smallest deterministic executable test that proves the
   behavior. Reserve manual playtest evidence for window, input, audio, visual,
   packaging, or environment behavior automation cannot establish.

## Evidence Rules

- Separate confirmed gaps from inferred risks and state confidence.
- Cite production and test `path:line` evidence.
- Explain what current assertions prove and what remains unproved.
- Verify absence across both executable suites and relevant QA documentation.
- Do not import or run heuristic coverage-mapping scripts.
- Do not change code unless the request explicitly includes implementing tests.

## Report

List prioritized findings with gap, current coverage, evidence, confidence, and
a precise proposed `Run` case. Then include checks run, checks skipped, manual
evidence required, and strong existing coverage worth preserving. If no
meaningful gap is found, say so and identify residual manual risk.

## Attribution

Concise Cowbania adaptation of GitHub's MIT-licensed `test-gap-audit` skill,
pinned at commit `4f4796f0bf30e105700f97ed8408c12b6aa95e06`.
