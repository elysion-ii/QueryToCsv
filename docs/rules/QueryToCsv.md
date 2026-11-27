---
status: active
created: 2026-07-27
---

# QueryToCsv Rules

Application-specific rules for QueryToCsv. Write only deltas and overrides against the
shared and stack rules in this directory — never copy their text here. On conflict, this
file wins.

## Repository Language

- This is a public repository. English is the language it publishes in: console output,
  every other user-facing string, and all documentation
- `AGENTS.md`, this file, and all other agent instruction files in this repository are
  written in **English only**
- Keep rules concise and declarative. Do NOT include concrete code examples unless
  absolutely necessary — reference the relevant source file/method instead

## Required Input

Overrides Configuration Values in `configuration.md` on two points: which settings carry
no default, and which default substitutions are reported.

- **`Connections` is the only setting without a default.** No built-in value can name
  the server an operator means to query, so a configuration with no entry — or with an
  entry whose `Name` or `ConnectionString` is blank — ends the run with exit code 1.
  Every other setting, including the folder settings, falls back to its default
- **An absent setting is not a rejected value.** `configuration.md` reports every
  substitution, a missing value included; here only a value that is present and unusable
  is reported, and a key the operator left out takes its default silently

## Server Certificate Validation

Overrides `db.md` (Encrypted connections verify the server) and `security.md` (Protect
sensitive data in transit) on one point only: an operator may set
`TrustServerCertificate=True` in a connection string.

- **Why**: a SQL Server that uses its self-generated certificate regenerates it on every
  service restart, so the certificate can be neither trusted through a root nor pinned
  with `Server Certificate`. Rejecting the setting leaves such an installation unusable
  until the server is given a real certificate, and whether to accept the interception
  risk meanwhile is the operator's decision, not the tool's
- **The risk is never silent**: every run that is about to connect warns, once per
  entry, that the entry accepts any server certificate, naming the entry and never
  printing its connection string (QTC-7 in `docs/specs/QueryToCsv.md`). The warning
  depends on the setting alone, never on how the driver combines it with other keywords
  (e.g., `Encrypt=Strict`), because that driver behavior cannot be verified against the
  test-run server, and an unverified exemption could let an unvalidated connection pass
  without a warning
- **Everything else stays rejected**: turning encryption off, and putting a user name or
  password in the connection string, still end the run. The application never sets
  `TrustServerCertificate` itself, and the shipped template and documented examples keep
  the validated form

## Application Rules

- **Only SELECT statements may reach the server.** `QueryExecutor.IsSelectOnly` parses
  the text as T-SQL and accepts only what QTC-15 in `docs/specs/QueryToCsv.md`
  states. The decision is made on the parse tree, never on keywords found in the text,
  because T-SQL places one statement after another without any separator: a keyword list
  cannot see where the `SELECT` ends and the next statement begins. A change that widens
  what the tool may execute is a specification change first, never an inline relaxation
  of the check

## Release Constraints

- Every `<Version>` change in `Directory.Build.props` must be published as an annotated
  tag `v{version}` on `main` and a matching GitHub Release; the procedure is
  `docs/guides/release.md`
- Every release tag must have a corresponding GitHub Release
- Release assets must be produced with `build/Build.ps1` and `build/Installer.ps1`, and
  every GitHub Release must include both `build/QueryToCsv/QueryToCsv.exe` and
  `build/Installer/QueryToCsv-Setup-{version}.exe`
- Only the current version stays published: once a new release is verified, every
  previous version's tag and GitHub Release must be deleted as pairs, locally and on
  `origin`, so that no tag outlives its release
