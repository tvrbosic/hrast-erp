---
name: commit
description: Use when user wants to commit and push changes - analyzes uncommitted changes, generates a commit message, presents it for approval, then commits and pushes on approval.
---

# Smart Commit

## Overview

Analyze uncommitted changes, generate a descriptive commit message, present it for user approval, then commit and push on approval.

## Steps

### 1. Analyze changes

Run these in parallel:
- `git status` — see which files are modified/untracked
- `git diff` — see unstaged changes
- `git diff --staged` — see already-staged changes
- `git log -5 --oneline` — check existing commit message style in repo

### 2. Generate commit message

Follow **Conventional Commits** format:

```
type(scope): short summary under 100 chars

Optional body explaining WHY (not what), if changes are complex.
```

**Types:**
| Type | Use for |
|---|---|
| `feat` | New feature |
| `fix` | Bug fix |
| `refactor` | Code restructure, no behavior change |
| `chore` | Build, deps, tooling, scaffolding |
| `docs` | Documentation only |
| `test` | Adding or fixing tests |
| `style` | Formatting, no logic change |

**Scope** = affected area, e.g. `feat(inventory)`, `fix(auth)`, `chore(api)`

### 3. Present for approval

Display the proposed message clearly and ask the user to:
- **Approve** — proceed as-is
- **Edit** — provide revised message, then proceed
- **Cancel** — stop, do not commit

Do NOT commit until explicit approval is given.

### 4. Commit and push (on approval)

1. Stage files — prefer specific file names over `git add .` to avoid accidentally including secrets or build artifacts
2. Commit with the approved message — do NOT append any co-author trailer
3. Push to remote: `git push`
4. Confirm success with final `git status`

## Rules

- Never commit without explicit user approval of the message
- Never use `--no-verify` or skip hooks
- Never force push unless explicitly asked
- Never commit `.env`, credentials, or secret files — warn the user if these appear in the diff
- If push fails (e.g. remote has new commits), report the error and ask how to proceed — do not auto-rebase or auto-merge
