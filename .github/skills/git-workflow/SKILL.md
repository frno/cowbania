---
name: "git-workflow"
description: "Cowbania direct-main workflow for the current prototype phase"
domain: "version-control"
confidence: "high"
source: "user-directive"
---

## Context

Cowbania currently uses a direct-to-`main` workflow. The remote repository is
being established and the maintainer has explicitly chosen to defer feature
branches and pull requests until the project needs them.

This is a repository-specific temporary policy. Continue using it until the
maintainer explicitly restores a branch or pull-request workflow.

## Workflow

1. Start work on `main`.
2. Before editing, confirm the checkout is on `main` and inspect the working
   tree so unrelated changes are preserved.
3. Make the requested changes and update directly related tests and
   documentation.
4. Run the smallest complete validation for the change.
5. Stage only the intended repository changes.
6. Commit directly to `main` with a concise conventional commit message and
   the required Copilot co-author trailer.
7. Push `main` to `origin`.

```powershell
git switch main
git pull --ff-only origin main
# Make and validate changes.
git add <intended-paths>
git commit -m "<type>: <summary>"
git push origin main
```

## Concurrent Work

- Prefer sequential edits in the main checkout.
- When multiple agents must edit simultaneously, assign non-overlapping file
  ownership and prohibit global `stash`, `clean`, `restore`, `reset`, and broad
  staging commands.
- Worktrees may be used for filesystem isolation, but completed work must be
  integrated into local `main`; do not push temporary branches unless the user
  explicitly requests it.
- Never switch branches while another agent is actively editing the shared
  checkout.

## Safety

- Never overwrite or discard uncommitted user or agent changes.
- Never force-push or rewrite published history.
- Fetch before pushing and stop on non-fast-forward conflicts.
- Do not create a pull request or feature branch unless the user explicitly
  asks for one.
- Keep commits cohesive and run validation before each push.

## Restoring a Branch Workflow

When the maintainer asks to restore feature branches or pull requests, update
this skill before applying the new workflow.
