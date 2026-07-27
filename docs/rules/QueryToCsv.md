---
status: active
created: 2026-07-27
---

# QueryToCsv Rules

Application-specific rules for QueryToCsv. Write only deltas and overrides against the
shared rules (`standard.md`, `dotnet.md` in this directory) — never copy shared rule
text here. On conflict, this file wins.

## Repository Language

- This is a public repository. English is the primary language for everything the
  repository publishes: console output and other user-facing strings, code comments,
  documentation, and commit messages
- `AGENTS.md`, this file, and all other agent instruction files in this repository are
  written in **English only**
- Keep rules concise and declarative. Do NOT include concrete code examples unless
  absolutely necessary — reference the relevant source file/method instead

## Application Rules

- **Only SELECT statements may reach the server.** `QueryExecutor.IsSelectOnly` strips
  comments and string literals, then rejects the statement if any data-modifying or
  out-of-scope keyword remains. A change that widens what the tool may execute is a
  specification change first (`docs/specs/QueryToCsv.md`), never an inline relaxation of
  the keyword list
- **Never commit a real `appsettings.json`.** It carries connection strings and is
  gitignored; `QueryToCsv/appsettings.sample.json` is the only configuration file in the
  repository, and every value in it stays a placeholder
