---
name: he-code-comments
description: Use when writing or changing C# code, including building new features, adding methods or classes, and refactoring, and when adding, rewriting or reviewing code comments or XML doc comments (/// summary, remarks), including when asked to make a C# file easier to read or understand.
---

# Code Comments

## Overview

Comments are written for a developer reading the file from top to bottom. Each comment explains the line, block or step **directly below it**. Apply this skill to every C# method you write or change, not only when comments are the task. This skill works in any C# repository.

## Step 0: Learn the repository's conventions

Before you write or review comments:

1. Read the agent guide if there is one (`CLAUDE.md`, `AGENTS.md`, `.github/copilot-instructions.md`) and any `docs/` folder. Look for rules about comments.
2. Check `.editorconfig` and `Directory.Build.props` for doc analyzer rules: `SA1600`, `CS1591`, `GenerateDocumentationFile`. If one of them requires XML docs, follow it.
3. Open 2–3 files that already have comments and match their tone, line width and style.

Where the repository's rules differ from this skill, follow the repository.

## Step comments

A reader should understand a method by reading only its comments, in order, like pseudocode, then read the code of the one step they care about.

**When:** the method body is longer than 15 lines, or it does two or more separate things in a row (load, check, transform, send, clean up). This applies to production code and tests. A method of 15 lines or fewer that does one thing gets no step comments.

**How many:** one step comment per step. A method longer than 15 lines has at least 2. A 30-line method usually has about 3.

**How to write one:**
- Put it directly above the first line of its step, with a blank line before the step.
- Describe what the whole step is for, not each statement: `// Load the station and stop if it is unknown or disabled`, not `// call FindAsync` + `// check for null`.
- Start with a plain verb (Load, Check, Build, Send, Retry, Clean up) and keep it to one line.
- Add a *why* when the step would surprise the reader: `// Drain the queue first so stale requests from a previous run are not answered`.
- Read all step comments together. They must tell the method's story from start to end, with no step missing and no step described twice.

**In tests**, the steps are setup, action and check. A test longer than 15 lines labels them `// Arrange: …`, `// Act: …`, `// Assert: …`, each followed by what that part does (`// Arrange: seed a role named "Auditor"`). Use these labels even if the repository's existing tests separate the parts with blank lines only. Tests of 15 lines or fewer keep the blank-line separation without comments.

Example:

```csharp
public async Task<Result> SyncAsync(string stationId, CancellationToken ct)
{
    // Load the station and stop if it is unknown or disabled
    var station = await _repository.FindAsync(stationId, ct);
    if (station is null || !station.Enabled)
    {
        return Result.Skipped(stationId);
    }

    // Fetch the readings recorded since the last sync
    var since = station.LastSyncedAt ?? DateTimeOffset.MinValue;
    var readings = await _client.GetReadingsAsync(stationId, since, ct);

    // Group readings by sensor; each group becomes one batch message
    var batches = readings
        .GroupBy(r => r.SensorId)
        .Select(g => new ReadingBatch(stationId, g.Key, g.ToList()));

    // Publish the batches
    foreach (var batch in batches)
    {
        await _publisher.PublishAsync(batch, ct);
    }

    // Record the sync time so the next run fetches only newer readings
    station.LastSyncedAt = DateTimeOffset.UtcNow;
    await _repository.SaveAsync(station, ct);
    return Result.Ok(stationId);
}
```

## Detail comments (`//`)

Step comments give the outline. A detail comment explains one line or small block inside a step, and is added only when it does one of these jobs:

| Job | Example |
|---|---|
| **What it does**, when the line itself is hard to read | `// Matches "ABC-123" and "ABC123"; the dash is optional` |
| **Why**, when the reader would expect something simpler | `// Polled instead of awaited: the client library has no completion callback` |
| **Warning / invariant** that a later edit could break | `// Every StartAsync must finish before its instance is disposed, or a connection could leak` |
| **Assumption** the code depends on | `// Assumes IDs are unique per gateway, which the config validator checks` |
| **Justification** for a workaround | `// Swallowed on purpose; the caller rethrows the original failure` |
| **Meaning of opaque values** (magic numbers, positional parameters, sentinels) | `// 0 = no timeout; the broker closes idle channels itself` |

A good detail comment:
- Sits directly above the code it explains and covers only that code. It is 1–3 lines long. A statement with several parts (a SQL batch, a builder chain) gets one comment with one line per point.
- Uses plain words; concrete examples help (e.g. `"PRECONDITION_FAILED"`).
- Says only what the code, the project docs or the called code actually show. Read the called code before you claim how it behaves.

**Suppressions:** prefer `[SuppressMessage("Category", "ID:Title", Justification = "...")]` and put the reason in `Justification`. If `#pragma warning disable` is needed (e.g. to suppress a single statement rather than a whole member), put the reason directly above the `#pragma`.

A step comment and a detail comment can sit next to each other when a step's first line also needs a detail. Put the step comment first.

## XML doc comments (`///`)

Give a type a `<summary>` when any of these is true:

1. **It is reused or global:** base classes, interfaces, types in shared libraries, utility classes with several helpers, `*ServiceExtensions` and other registration classes, middleware, pipeline behaviors, interceptors, shared serializer settings.
2. **It runs a multi-step flow across several methods** (hosted services, supervisors, auth flows, recurring jobs). Add a `<remarks>` with numbered steps for the flow *between* methods. Step comments inside each method cover the steps *within* that method. Don't list the same steps in both places.
3. **It is a shared contract that maps an external wire format** (messages or DTOs shared by several projects, or that mirror another system's schema). Say what it represents and what it maps from, e.g. `Wire contract: <System> <MessageName>.<path>`.

Otherwise, **do not add an XML doc**. This covers handlers, commands, queries, validators, internal DTOs and responses, entity configurations, controllers, error catalogs and tests. Their name and folder already say what they are. Their methods still get step comments when the rules above call for them.

**Members:**
- On a documented type, add `///` to a public member only when its signature doesn't tell the whole story: "returns null on failure, never throws", units (`in milliseconds`), which type an ID refers to, a field that is guessed or not confirmed by the contract, polymorphic or serialization behavior.
- Skip `///` on private members. Use step comments inside them instead.

## Generated code

Never add or edit comments in generated code: files marked `// <auto-generated />` and tool-scaffolded files that lack the marker, such as EF Core migrations (`Migrations/*.cs`, including their `Up()` / `Down()` methods).

## When reviewing comments

- List each problem as `file:line`, the matching row from the table below, and the fix.
- List methods that need step comments but have none.
- Apply the fixes only after the user approves, unless they asked you to fix them directly.

## Common mistakes

| Mistake | Fix |
|---|---|
| A method longer than 15 lines with no step comments, or only one at the top | Add one step comment per step. |
| A comment on every statement | Merge them into one step comment per step. Keep only detail comments that do a job from the table. |
| One step comment that covers two steps (`// Publish the batches, then save`) | Split it into two comments, one above each step. |
| `<remarks>` steps repeated as step comments in the same method | Keep the flow between methods in `<remarks>` and the steps inside a method as step comments. |
| `/// <summary>Handles CreateOrderCommand…` on a handler | Remove it. Put a non-obvious *why* in a detail comment above the code it explains. |
| `/// <summary>Gets the ID.</summary>` on a trivial property | Remove it. Keep a member doc only when it adds meaning (units, references, guessed fields). |
| A detail comment that restates one statement (`// increment total`, `// return success`) | Delete it. |
| A comment answers a question no reader asks | Say what the next lines do, or move the point to where it matters (for example the suppression's `Justification`). |
| A claim about another component that sounds right but wasn't checked | Check the code, or leave it out. |
| History (`// changed because…`, `// previously…`) | Put it in the commit message instead. |
| Step comments added to a generated file or EF migration | Remove them; generated code is never edited. |
