---
name: sync-docs
description: Use when you want to reflect recent code changes into project documentation — README.md, CLAUDE.md, or docs/ files. Call as /sync-docs (last 1 commit) or /sync-docs N (last N commits).
---

# sync-docs

## Overview

Scan the last N commits, determine which changes are worth documenting, decide which documentation targets should be updated, show the proposed changes to the user for review, then apply them after approval.

Default: 1 commit. Pass a number to scan more: `/sync-docs 3`.

---

## Documentation Targets

Understand the purpose of each target before deciding what belongs where.

### README.md
High-level project overview for someone arriving at the repo for the first time. Should cover:
- What the project is and why it exists
- Architecture overview (modular monolith, Clean Architecture)
- Module list
- Project structure (directory layout)
- Technology stack
- How to build and run

**Update when:** New modules added, major architectural changes, new technologies or dependencies introduced, project structure reorganized, build/run instructions change.

**Do NOT add:** Implementation details, pattern explanations, developer workflows, or Claude-specific conventions.

### CLAUDE.md
Developer guide specifically for Claude Code working in this repository. Contains:
- Common commands (build, test, run)
- Architecture rules and constraints (e.g. inward-only dependencies, naming conventions)
- SharedKernel type catalog — every key type with its intended use
- Conventions Claude must follow (e.g. Result pattern, exception types, DI wiring)

**Update when:** New SharedKernel types added, architectural rules established or changed, new conventions introduced, naming patterns updated, new commands or test patterns added.

**Do NOT add:** High-level project descriptions (that's README), pattern tutorials (that's docs/), or one-time implementation notes.

### docs/
In-depth reference files for things that are not self-explanatory. Two categories:

**Pattern/concept explanations** — for abstractions or patterns a developer might not know:
- What it is, why it exists, how it works in this codebase
- Example: `shared-kernel.md` explains AggregateRoot, ValueObject, Result pattern, etc.
- Create or update when a non-obvious abstraction is introduced or significantly changed

**Development flow guides** — step-by-step walkthroughs for recurring tasks every developer will eventually do:
- Example: `adding-entities.md` walks through domain entity → EF config → migration
- Create when a new multi-step workflow becomes standard practice in the project
- Update when the steps change (new layers, new conventions, changed tooling)

**File naming:** use kebab-case, describe the topic or action (`shared-kernel.md`, `adding-entities.md`, `implementing-commands.md`).

---

## Flow

### Step 1 — Gather commit data

Run in parallel:
```bash
git log -N --oneline                          # N = argument or 1
git diff HEAD~N HEAD --stat                   # which files changed
git diff HEAD~N HEAD                          # full diff
```

### Step 2 — Analyze changes

For each commit, identify:
- What was added, changed, or removed (types, classes, files, configs)
- Is this a new abstraction a developer needs to understand?
- Is this a new workflow or convention that developers will repeatedly follow?
- Does it affect project structure, technology stack, or build/run process?
- Does it introduce or change conventions Claude must follow when working here?

### Step 3 — Filter: what is worth documenting?

**Document:**
- New SharedKernel types or significant changes to existing ones
- New architectural patterns or rules
- New modules, layers, or major structural changes
- New or changed development flows (adding entities, wiring modules, etc.)
- New technologies, dependencies, or tooling
- New conventions for naming, DI, error handling, etc.

**Skip:**
- Bug fixes with no behavioral or structural change
- Internal implementation details (private method changes, refactors with same API)
- Test additions (unless they reveal a new testing convention worth documenting)
- Formatting or style-only changes
- Changes already accurately documented

### Step 4 — Decide targets

For each change worth documenting, map it to one or more targets:

| Change type | Target |
|---|---|
| New SharedKernel type or significant change | CLAUDE.md + docs/shared-kernel.md |
| New architectural rule or constraint | CLAUDE.md |
| New module added | README.md + CLAUDE.md |
| Project structure change | README.md |
| New technology or dependency | README.md |
| New development flow (multi-step, recurring) | docs/ (new or existing file) |
| Non-obvious pattern needs explanation | docs/ |
| Build/run command change | CLAUDE.md + README.md |

### Step 5 — Draft changes

For each target, draft the exact content additions or edits. Be precise:
- Quote the new section or paragraph you will add
- For edits to existing content, show the before and after
- For new docs/ files, show the full proposed content

### Step 6 — Present for approval

Display a clear summary:

```
## Proposed documentation changes

### README.md
[what will change and why]

### CLAUDE.md
[what will change and why]

### docs/shared-kernel.md (existing)
[what will change and why]

### docs/implementing-commands.md (NEW)
[full proposed content]
```

Then ask the user to:
- **Approve all** — apply everything as shown
- **Approve some** — user specifies which targets to apply
- **Edit** — user provides corrections, then re-confirm
- **Cancel** — stop, apply nothing

Do NOT apply any changes until explicit approval is given.

### Step 7 — Apply changes

Apply only the approved changes. After writing each file, confirm what was updated.

---

## Rules

- Never apply changes without explicit user approval
- Never add speculative or forward-looking content ("in the future we will...")
- Never duplicate content across targets — each piece of information belongs in one place
- Keep README.md high-level; keep details in CLAUDE.md or docs/
- Keep CLAUDE.md actionable for Claude; keep tutorials in docs/
- Prefer updating existing docs/ files over creating new ones unless the topic is clearly distinct
- New docs/ files should cover topics broad enough to be referenced repeatedly, not one-time notes
- Do not touch documentation that is unrelated to the scanned commits
