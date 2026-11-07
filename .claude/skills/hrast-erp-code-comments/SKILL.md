---
name: hrast-erp-code-comments
description: Use when writing, adding, rewriting, or reviewing code comments or XML doc comments (/// summary, remarks) in HrastERP C# files, including when asked to make a file easier to read or understand.
---

# HrastERP: Code Comments

## Overview

A comment is written for a developer reading the file top to bottom. Each one helps that reader understand the line, block, or few lines **directly below it**.

## Inline comments (`//`)

An inline comment does one of these jobs for the code below it:

| Job | Example |
|---|---|
| **What it does**, when the code alone doesn't show it at a glance | `// Find every soft-deletable entity type; each one is a table to purge` |
| **Why**, when the reader would expect something simpler | `// No DELETE ... LIMIT in Postgres, so the batch is chosen by subquery` |
| **Warning / invariant** a future edit could break | `// Order must match the SELECT values in the purge SQL` |
| **Assumption** the code relies on | `// Assumes a single-column key, which BaseEntity<TId> guarantees` |
| **Justification** for a suppression or workaround | `// EF1002 warns that interpolated SQL may allow injection. Only table/column names from the EF model are interpolated; every value is a parameter.` |
| **Meaning of opaque values** (magic values, positional params, `Guid.Empty`) | `// {3} user id (Guid.Empty = system job, no user)` |

Shape of a good inline comment:
- Sits directly above the code it explains, and only that code. If two comments describe one block, merge them; if one comment covers unrelated lines, reorder the code or split the comment.
- 1–3 lines for a single line or small block. A multi-part statement (e.g. a SQL batch) gets one line per point it needs explained, in one comment above it.
- A comment justifying a `#pragma` goes directly above the `#pragma`.
- Plain words. Concrete examples help (`e.g. "Tenant"`).
- States only what the code, the project docs (AGENTS.md, `docs/`), or the code it calls actually show. Read the called code before claiming how it behaves.

## XML doc comments (`///`)

Add a `<summary>` to a type when **either** is true:
1. **Reused / global** — other code builds on it or calls it from many places: base classes (`AggregateRoot`, `BaseEntity`), SharedKernel types, interfaces, utility classes with several helpers, `*ServiceExtensions`, pipeline behaviors, interceptors, middleware.
2. **Non-obvious multi-step flow** — a reader needs the big picture before the code makes sense (recurring jobs, `AuthService`). Add a `<remarks>` with numbered steps for these.

Otherwise, **no XML doc**. This includes command/query handlers, commands, queries, validators, DTOs/responses, entity configurations, controllers, and `<Module>Errors` catalogs — their name and folder already say what they are.

Members: add `///` on public members of reused types when behavior isn't obvious from the signature (e.g. "returns null on failure, never throws"). Skip private members.

Split of work: the XML doc covers the type's purpose, its overall flow, and why it is built this way; inline comments cover the details of individual statements. Don't repeat the summary inline.

## Common mistakes

| Mistake | Fix |
|---|---|
| `/// <summary>Handles DeleteTenantCommand…` on a handler | Remove. Put the non-obvious *why* as an inline comment above the check it explains. |
| Inline comment restates the class summary (`// Switch DELETE to UPDATE` under a summary saying the same) | Keep one. |
| Answering a question no reader asks (`// Names come from EF metadata, not user input`) | Say what the next lines do, or move the point to where it matters (the `#pragma`). |
| Plausible but unverified claims about other components | Check the code, or leave it out. |
| History (`// changed because…`, `// previously…`) | Belongs in the commit message. |
| Restating the code (`// increment total`, `// return success`) | Delete. |
