# QueryToCsv — Agent Instructions

QueryToCsv is a Console application targeting `net10.0`. It connects to Microsoft SQL
Server, runs a `.sql` file or an inline query, and writes the result set to CSV.

`CLAUDE.md` at the repository root is a one-line import of this file. This file is the
repository's router — facts, commands, and reading instructions; it holds no rule text.
Rule bodies live under `docs/rules/`. Edit this file, never `CLAUDE.md`.

## Technology Stack

| Item | Detail |
|------|--------|
| Language | C# |
| Runtime | net10.0 |
| UI | CLI (no UI) |
| Database | Microsoft SQL Server via `Microsoft.Data.SqlClient` |
| CSV | CsvHelper (RFC 4180) |
| Logging | NLog, daily rotation under `logs/` next to the executable |
| Distribution | Self-contained single EXE + Inno Setup installer |

## Applications

| Application | Projects | Rules | Specification |
|---|---|---|---|
| QueryToCsv | QueryToCsv, QueryToCsv.Tests | `docs/rules/QueryToCsv.md` | `docs/specs/QueryToCsv.md` |

## Rules and AUDIT

- **Before implementing any change**, read in order: `docs/rules/standard.md` (shared core), `docs/rules/dotnet.md` (.NET rules), then the application's rules file and specification from the Applications table. On conflict the more specific file wins (application > language > core)
- **When the application being changed exposes a command-line interface** (a console application, or a GUI application that accepts command-line options), also read `docs/rules/cli.md`
- **Before creating, changing, moving, renaming, archiving, or deleting any document**, also read `docs/rules/documentation.md`
- **Before any Git write operation or PR operation** (commit, branch, push, PR creation, update, or merge), also read `docs/rules/git.md`
- **When a change requires behavior not in the specification**, spec-first applies — read the Specifications section of `docs/rules/standard.md` before implementing
- **When transitioning from a plan to implementation**, re-read this file (root and any nested `AGENTS.md` covering the work area) and the rules files first, so all rules are loaded before code is written
- **Before reporting an implementation task as complete**, run the AUDIT procedure at the end of `docs/rules/standard.md`
- `docs/rules/standard.md`, `docs/rules/documentation.md`, `docs/rules/git.md`, `docs/rules/cli.md`, and `docs/rules/dotnet.md` are managed by dev-standards — never edit them; repository- and application-specific rules go in `docs/rules/QueryToCsv.md`

## Commands

| Purpose | Command |
|---------|---------|
| Format | `dotnet format QueryToCsv.slnx` |
| Format check (must pass before completion) | `dotnet format QueryToCsv.slnx --verify-no-changes` |
| Build | `dotnet build QueryToCsv.slnx -c Release` |
| Test | `dotnet test QueryToCsv.Tests/QueryToCsv.Tests.csproj` |
| Full build (format gate → tests → publish) | `build/Menu.bat` (interactive) or `powershell -ExecutionPolicy Bypass -File build/Build.ps1` |
| Installer | `powershell -ExecutionPolicy Bypass -File build/Installer.ps1` |
| Release | see `docs/guides/release.md` |

## Directory Layout

### `QueryToCsv/`

The main project. Contains all application source code.

- `Program.cs` — entry point: help, `--open`, one-liner mode, interactive flow, NLog setup
- `CliRunArgs.cs` — command-line parsing for one-liner mode
- `AppSettings.cs` — `appsettings.json` loading, path resolution, validation
- `ConsoleUi.cs` — interactive prompts and CSV encoding resolution
- `QueryExecutor.cs` — SELECT-only check, query execution, CSV writing
- `appsettings.sample.json` — the template shipped as the initial `appsettings.json`; the real `appsettings.json` holds connection strings and is gitignored

### `QueryToCsv.Tests/`

xUnit test project.

### `build/`

Build scripts and installer configuration.

- `Build.ps1` runs format verification (`dotnet format --verify-no-changes`) and tests, then publishes QueryToCsv as a self-contained single-file EXE to `build/QueryToCsv/`. Both gates must pass before publish proceeds. It also stages `appsettings.json` (from the sample) and the `queries/`, `output/` folders that the installer ships
- `Installer.ps1` invokes Inno Setup (ISCC.exe) on `Setup_QueryToCsv.iss`; requires `build/QueryToCsv/QueryToCsv.exe` to exist. Before running ISCC it reads `<Version>` from `Directory.Build.props`, injects it via `/DMyAppVersion`, and — if `CHANGELOG.md` exists — verifies it contains a heading for the current version (fails otherwise). The version and CHANGELOG rules are `docs/rules/dotnet.md` VERSION
- Output directories: `build/QueryToCsv/` (self-contained EXE) and `build/Installer/` (installer package), both gitignored (rules: `docs/rules/dotnet.md` OUTPUT)

### `docs/`

All non-source documents, placed in role-based subfolders (`rules/`, `adr/`, `specs/`,
`guides/`, `references/`, `investigations/`, `notes/`, `plans/`, `inbox/`, `archive/`).
Before creating, changing, moving, renaming, archiving, or deleting any document — or
when unsure where one belongs — read `docs/rules/documentation.md` (also distributed
in this repository) first; it defines placement, naming, and front matter.

- `docs/rules/` — rule bodies: `standard.md`, `documentation.md`, `git.md`, `cli.md` + `dotnet.md` (managed by dev-standards) and `QueryToCsv.md` (application rules)
- `docs/specs/QueryToCsv.md` — the QueryToCsv specification: what it does
- `docs/guides/` — repository-specific procedures, including `release.md`
- `docs/adr/` — Architecture Decision Records; retired ADRs move to `docs/adr/archive/`
- `docs/plans/` and `docs/archive/plans/` — working area for plans, gitignored (`docs/rules/documentation.md`, `docs/plans/`)

### Runtime layout (installed application)

`queries/`, `output/`, and `logs/` sit next to the executable and are resolved through
`AppContext.BaseDirectory`. `QueryFolder` and `OutputFolder` in `appsettings.json` may
point elsewhere; relative values resolve against the executable's directory.
