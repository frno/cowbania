---
name: "test-discipline"
description: "Use only for explicit maintenance of Cowbania's custom executable test harnesses: test registration, inventory/count assertions, stale or missing case diagnosis, or changes inside test infrastructure. Do not load merely because normal implementation work includes tests."
domain: "quality"
confidence: "high"
source: "earned (Fenster/Hockney incident, test assertion sync violations)"
---

## Harness maintenance

- Register every case through the harness's `Run("...", () => ...)` inventory so it executes and is reported.
- Keep counted disk inventories, expected filenames, and registration/count assertions synchronized with the cases or assets they enumerate.
- Use focused assertions that prove outcomes and boundaries rather than mere execution.
- Keep the executable harness dependency-free; do not introduce an external test framework.
- Diagnose stale, missing, skipped, or double-registered cases at the inventory and runner level.

For normal implementation tests and validation, follow `cowbania-deterministic-monogame` rather than duplicating its guidance or commands.
