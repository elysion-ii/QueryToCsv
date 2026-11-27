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
| SDK | .NET 10 SDK, selected by `global.json` (10.0.404 or a later 10.0 feature band, never a later major version); CI installs the SDK from the same file |
| UI | CLI (no UI) |
| Database | Microsoft SQL Server via `Microsoft.Data.SqlClient` |
| CSV | CsvHelper (RFC 4180) |
| Logging | NLog, one file per day under `logs/` next to the executable, falling back to the user's local application data when that folder cannot be written to |
| Distribution | Self-contained single-file publish + Inno Setup installer; the published file set follows from the native dependencies (`docs/rules/dotnet-client.md`, NATIVEDEP) |

## Applications

| Application | Projects | Rules | Specification |
|---|---|---|---|
| QueryToCsv | QueryToCsv, QueryToCsv.Tests | `docs/rules/QueryToCsv.md` | `docs/specs/QueryToCsv.md` |

## Rules and AUDIT

Rules in `docs/rules/` are organized into three tiers:
- **Application rules**: `docs/rules/QueryToCsv.md` (this repository's own rules and overrides)
- **Stack rules**: `docs/rules/dotnet.md`, plus every .NET facet rule file under `docs/rules/` (e.g., `dotnet-client.md`) (managed by dev-standards)
- **Shared rules**: Universal engineering standards (`docs/rules/standard.md`, `docs/rules/documentation.md`, `docs/rules/specs.md`, `docs/rules/testing.md`), plus every conditional shared rule file under `docs/rules/` (managed by dev-standards)

**Precedence on conflict**: Application rules > Stack rules > Shared rules.

- **Rule Scope**: which rule files and specification every change complies with — and when to report missing conditional shared rule domains — is stated in `docs/rules/standard.md` (How to Use These Rules). The specification of QueryToCsv is `docs/specs/QueryToCsv.md`.
- **Loading Rules**: Read this file, every applicable file under `docs/rules/`, `docs/specs/QueryToCsv.md`, and `docs/references/code-map.md` before writing code (including when transitioning from a plan to implementation).
- **Completion Gate (AUDIT)**: Before reporting an implementation task as complete, pass the AUDIT gate in `docs/rules/standard.md`.
- **File Ownership**: rule files under `docs/rules/` managed by dev-standards are copies of its pinned tag; repository- and application-specific rules are maintained in `docs/rules/QueryToCsv.md`.

## Commands

| Purpose | Command |
|---------|---------|
| Format | `dotnet format QueryToCsv.slnx` |
| Format check (must pass before completion) | `dotnet format QueryToCsv.slnx --verify-no-changes` |
| Build | `dotnet build QueryToCsv.slnx -c Release` |
| Test | `dotnet test QueryToCsv.Tests/QueryToCsv.Tests.csproj` |
| End-to-end test | none yet (`docs/rules/testing.md`, Test Layers) |
| Full build (gates → publish) | `build/Menu.bat` (interactive) or `powershell -ExecutionPolicy Bypass -File build/Build.ps1` |
| Installer | `powershell -ExecutionPolicy Bypass -File build/Installer.ps1` |
| Release | see `docs/guides/release.md` |

## Code Map

Feature folders and shared mechanism homes in QueryToCsv are listed in `docs/references/code-map.md` (`docs/rules/documentation.md`, Code Map; `docs/rules/standard.md`, One Home per Piece of Knowledge).

## Directory Layout

### `QueryToCsv/`

The main project. Contains all application source code.

### `QueryToCsv.Tests/`

xUnit test project.

### `build/`

Build scripts and installer configuration.

- `Build.ps1` runs the configuration-file check, format verification (`dotnet format --verify-no-changes`), requirement-coverage check, and tests, then publishes QueryToCsv as a self-contained single-file EXE to `build/QueryToCsv/`. All checks must pass before publish proceeds. It also stages `appsettings.json` (from `appsettings.template.json`) and the `queries/`, `output/` folders that the installer ships
- `Menu.bat` is an interactive menu over `Build.ps1` and `Installer.ps1`
- `Installer.ps1` invokes Inno Setup 6.3 or later (ISCC.exe) on `Setup_QueryToCsv.iss`; requires `build/QueryToCsv/QueryToCsv.exe` to exist. Before running ISCC it reads `<Version>` from `Directory.Build.props` (`docs/rules/dotnet.md`, VERSION), injects it via `/DMyAppVersion`, and — if `CHANGELOG.md` exists — verifies as its release packaging gate that `CHANGELOG.md` contains a heading for the current version (failing otherwise)
- Output directories: `build/QueryToCsv/` (publish output) and `build/Installer/` (installer package), both gitignored (`docs/rules/dotnet.md`, OUTPUT)

### `.github/workflows/`

- `ci.yml` — builds and tests on every push to `main` and every pull request targeting it
- `release.yml` — triggers on a `v*` tag: runs `build/Build.ps1` and `build/Installer.ps1` on a runner and creates the GitHub Release with this version's `CHANGELOG.md` section as its notes. Pushing the tag is what publishes; no asset is uploaded by hand (procedure: `docs/guides/release.md`)

### `docs/`

All non-source documents, placed in role-based subfolders (`rules/`, `adr/`, `specs/`,
`guides/`, `references/`, `investigations/`, `notes/`, `plans/`, `inbox/`, `archive/`).
Before creating, changing, moving, renaming, archiving, or deleting any document — or
when unsure where one belongs — read `docs/rules/documentation.md` (also distributed
in this repository) first; it defines placement, naming, and front matter.

- `docs/rules/` — rule bodies: universal shared rules (`docs/rules/standard.md`, `docs/rules/documentation.md`, `docs/rules/specs.md`, `docs/rules/testing.md`), `docs/rules/dotnet.md`, the conditional shared and .NET facet rule files distributed to this repository (all managed by dev-standards), and `docs/rules/QueryToCsv.md` (application rules)
- `docs/specs/QueryToCsv.md` — the QueryToCsv specification: what it does
- `docs/references/code-map.md` — feature folders and shared mechanism homes
- `docs/guides/release.md` — the release procedure
- `docs/adr/` — Architecture Decision Records; retired ADRs move to `docs/adr/archive/`
- `docs/plans/` and `docs/archive/plans/` — working area for plans, gitignored (`docs/rules/documentation.md`, Category Notes — `docs/plans/`)

### Runtime layout (installed application)

`queries/`, `output/`, and `logs/` sit next to the executable and are resolved through
`AppContext.BaseDirectory`. `QueryFolder` and `OutputFolder` in `appsettings.json` may
point elsewhere; relative values resolve against the executable's directory.
