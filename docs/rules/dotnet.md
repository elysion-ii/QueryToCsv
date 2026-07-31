# .NET Standards

Language rules file for .NET. Read together with `standard.md` (the language-agnostic
core) in the same directory. This file is token-free by design: concrete commands with
real project names (`dotnet format <App>.slnx`, ...) live in each repository's
`AGENTS.md`.

## Enforcement matrix

Rules marked **Enforced** are guaranteed mechanically by the project scaffold — a
violation fails the build — so AUDIT does not re-check them. Rules marked **AUDIT** have
no mechanical guard and must be checked during AUDIT. If the repository lacks the listed
mechanism (e.g., not yet retrofitted), treat that rule as an AUDIT item until the
mechanism is added.

| ID | Rule | Mechanism | Check |
|----|------|-----------|-------|
| FORMAT | Formatting follows `.editorconfig` | `dotnet format --verify-no-changes` gate in `Build.ps1` | Enforced — but run the formatter before completion (command in `AGENTS.md`) |
| ANALYZERS | Static analysis, warnings are errors | `Directory.Build.props`: `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, `TreatWarningsAsErrors` | Enforced |
| NAMESPACE | File-scoped namespaces only | `.editorconfig` (`csharp_style_namespace_declarations=file_scoped:warning`) + warnings-as-errors | Enforced |
| ASYNC | No blocking on async code; no async without await | VSTHRD analyzers (`Microsoft.VisualStudio.Threading.Analyzers`); CS1998 as error | Enforced |
| VAR | Type inference usage | `.editorconfig` suggestions only | AUDIT |
| STRING | Explicit `StringComparison` | — | AUDIT |
| ERROR | Error handling | — | AUDIT |
| CANCEL | CancellationToken plumbing | — | AUDIT |
| FILEPATH | Paths relative to the executable | — | AUDIT |
| LOGGING | Log location, rotation, retention, language | — | AUDIT |
| TEMPWORK | File operations via `%TEMP%` | — | AUDIT |
| CONSTANTS | No magic numbers/strings | — | AUDIT |
| COMMENTS | C# comment style | — | AUDIT |
| TESTNAME | xUnit test naming | — | AUDIT |
| VERSION | Single version definition + CHANGELOG release gate + clean displayed version | Installer script `#ifndef MyAppVersion → #error` guard; the installer build injects the version and gates on a CHANGELOG heading; `Directory.Build.props` sets `IncludeSourceRevisionInInformationalVersion=false` | AUDIT — the gates cover only the installer path; "no `<Version>` in a csproj" has no guard, and a deleted `<Version>` in `Directory.Build.props` falls back to the SDK default (1.0.0) unguarded |
| OUTPUT | Build outputs come only from the build scripts; never manual, never committed | Output directories are gitignored by the scaffold; `Build.ps1` gates publishing on the format check and the test suite | AUDIT — gitignore keeps outputs out of normal staging; it does not stop forced adds, manual additions, or hand-run publishing |
| SERIAL | One dotnet command at a time per solution | — | AUDIT — behavioral: observed at command-execution time, leaves no file artifact to check |

---

## FORMAT: Code Formatting

- **Style definition:** `.editorconfig` at the repository root is the single source of formatting rules. To change a style, edit `.editorconfig` — never deviate per file
- **Always run the formatter before completing implementation**, and do not report completion unless its check mode (`--verify-no-changes`) passes — the exact command is in `AGENTS.md`
- `Build.ps1` runs the same verification and fails the build on unformatted code

## ANALYZERS: Static Analysis

- `Directory.Build.props` enables the built-in Roslyn analyzers (`AnalysisLevel = latest-recommended`, `EnforceCodeStyleInBuild`, `TreatWarningsAsErrors`). **Warnings are build errors in every project**
- **Never disable or lower `TreatWarningsAsErrors` or `AnalysisLevel`.** To tolerate a specific warning, suppress it at the narrowest possible scope:

| Scope | Mechanism |
|---|---|
| A specific line | `#pragma warning disable <ID>` followed by `restore`. Reason comment required |
| A specific file/folder | `.editorconfig` section with `dotnet_diagnostic.<ID>.severity = none`. Reason comment required |
| A whole project | Add the ID to `<NoWarn>` in that csproj. Reason comment required |

- The reason comment states why this warning is acceptable here as a current constraint ("for now" or "fix later" is not a reason)

## ASYNC: Asynchronous Code

Refines the core's Synchronous vs Asynchronous section for .NET:

- **Never block on async code** (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`) — enforced by the VSTHRD analyzers (`Microsoft.VisualStudio.Threading.Analyzers` in `Directory.Build.props`)
- **Do not mark a method async when nothing in it awaits** — CS1998 is an error under warnings-as-errors
- When a suppression is genuinely justified (e.g., a synchronous entry point mandated by a framework), follow the ANALYZERS suppression tiers with a reason comment

## NAMESPACE: Namespaces

- **File-scoped namespaces only** (`namespace X;`) — enforced via `.editorconfig` + warnings-as-errors

## VAR: Type Inference

- **Use `var`** for `new` expressions and other obvious types
- **Use explicit types** when the type is not clear from the right-hand side

## STRING: String Comparison

- **Always specify `StringComparison`** for string methods (`StartsWith`, `EndsWith`, `IndexOf`, `Contains`, `Equals`)
- **Use `StringComparison.Ordinal`** for technical comparisons (paths, identifiers)
- **Use `StringComparison.OrdinalIgnoreCase`** when case-insensitivity is needed

## ERROR: Error Handling

- **No empty catch blocks:** Every `catch` must handle or log the exception
- **No raw exception messages in output:** Never display `ex.Message` directly to users
- **Don't log and rethrow:** The layer that handles an exception (catches without rethrowing) is responsible for logging it. A layer that rethrows must NOT log — each failure is logged exactly once

## CANCEL: Cancellation Token Propagation

- Every async method that performs potentially long I/O (DB, file, network, external process) MUST accept `CancellationToken ct = default` and pass it to every cancellable await — including static helpers and feature-internal classes, not only service entry points
- This applies even when no caller currently supplies a real token
- Do NOT pass `CancellationToken.None` where a `ct` variable is in scope
- Rationale: the project's `docs/adr/0001-cancellation-token-plumbing.md` (scaffolded into every project)

**Exempt — MUST NOT take a token (these operations must run to completion):**

| Category | Examples |
|---|---|
| Logging | logger implementations, exception-translation helpers |
| Transaction rollback | rollback helpers called from catch blocks |
| Best-effort cleanup | temp file deletion, post-failure cleanup |
| Post-commit finalization | file move/rename after a DB commit — aborting leaves files inconsistent with the committed data |

## FILEPATH: File Paths

- **Anchor every path to the executable's own location** — the application's files sit beside the executable, never wherever it happened to be started from. In .NET that anchor is `AppContext.BaseDirectory`
- **Do NOT use `Directory.GetCurrentDirectory()` or bare relative paths** — both resolve against the working directory, which changes with how the application is launched (shell, shortcut, scheduled task, another process)

## LOGGING: Log Output and Retention

### Location and format

- **Default:** a `Logs` directory beside the executable — in .NET, `{AppContext.BaseDirectory}\Logs\`
- **Fallback:** if that location is not writable, this application's own folder under the user's local application data — in .NET, `{Environment.GetFolderPath(SpecialFolder.LocalApplicationData)}\{AppName}\Logs\`
- **File name:** `{AppName}_yyyyMMdd.log` (daily rotation)
- **Log messages MUST be written in English**

### Retention

- **Every application deletes its own old logs.** A logging setup with no retention limit is a violation — logs are the one artifact an application grows without bound, on a machine nobody is watching
- **Default policy: daily rotation, the newest 30 files retained.** An application whose run frequency or log volume makes 30 the wrong number picks its own limit and states the value and the reason in its application rules file
- **Which axis to limit on follows the logging library's first-class parameter** (e.g. Serilog's `retainedFileCountLimit`). Under daily rotation a file-count limit and an age limit express the same policy; a total-size limit is an addition to one of them, never a replacement
- **Delete only this application's own log files**, matched by the file-name pattern above — never by sweeping the directory, which can hold files the application does not own
- **Cleanup is best-effort**: log the failure and continue. Failing to delete an old log never fails startup and never aborts a job
- **When the limit is configurable**, Configuration Values in `standard.md` applies — a missing, unparsable, or out-of-range limit falls back to the built-in default; it never disables cleanup and never throws

## TEMPWORK: File Operations in %TEMP%

When I/O targets may be network paths, perform all file reads/writes in local `%TEMP%`.

- **Input**: Copy source files from network to `%TEMP%` before processing
- **Output**: Create artifacts in `%TEMP%`, then `File.Copy` / `File.Move` to the final destination
- **Naming**: Flat under `Path.GetTempPath()` as `{FeatureName}_{Guid.NewGuid():N}.{ext}`
- **Cleanup**: Always delete in `finally`. Deletion failure is best-effort (log and continue)
- **Recleanup of leftover files**: Don't rely on the `finally` deletion alone — a killed process or crash can skip it, so retry cleanup elsewhere too
  - **Desktop apps**: On next startup, retry deleting leftover temp files older than a threshold (tune to the app's run interval)
  - **Console/long-running apps**: On startup, or at a suitable point within the processing loop (start, end, etc. — whatever fits the app's structure), retry deleting leftover temp files the same way

## CONSTANTS: Constants

- **No magic numbers/strings:** Extract hardcoded values to named constants
- **Application constants** as `const` in the owning class

## COMMENTS: Code Comments (C# specifics)

The comment philosophy (when to write, what never to write) is in `standard.md`.
C# specifics:

- **Language:** English
- **Style:** Simple inline comments (`//`). No XML documentation

## TESTNAME: xUnit Test Naming

- Test method names use `Method_Scenario_ExpectedResult` (e.g. `Merge_EmptyInput_ReturnsEmpty`)
- Underscore separation is the official convention; for this reason CA1707 is suppressed via `<NoWarn>` in the test project's csproj
- Test row ordering and the other testing principles are in the `standard.md` Testing section

## VERSION: Version Management

- **The version is defined ONLY in the `<Version>` tag of `Directory.Build.props`.** Never add `<Version>` to a csproj or `#define MyAppVersion` to the installer script — duplicate definitions are how versions drift out of sync. The EXE inherits it via MSBuild; the installer build injects it (the `.iss` fails with `#error` when it is not injected)
- **Version bump**: update `<Version>`, add a `## [x.y.z]` heading and entry to `CHANGELOG.md` if it exists, and include both in the **same commit**. Building the installer without the changelog heading fails at the gate
- **The installer build is the release packaging step** — it carries the CHANGELOG gate; the EXE that `Build.ps1` publishes is a build output the installer wraps, not a release
- Semantic Versioning (MAJOR.MINOR.PATCH); during development `0.x.x` (release as `1.0.0`); MINOR: new features, PATCH: bug fixes and small changes
- **Displayed version** (Version Display in `standard.md`, VERSIONOUT in `cli.md`): read `AssemblyInformationalVersionAttribute` — it equals `<Version>` because `Directory.Build.props` sets `IncludeSourceRevisionInInformationalVersion=false`, which stops the SDK from appending `+<git sha>`. Never remove that property while the displayed version must stay `AppName X.Y.Z`

## OUTPUT: Build Outputs

- **Never manually add files to build output directories, and never commit them** — outputs are produced only by the build scripts (the scaffold gitignores the output directories; `AGENTS.md` names them)
- **Produce distributables only via the build scripts** — `Build.ps1` runs the format check and the test suite before publishing; invoking `dotnet publish` by hand skips those gates

## SERIAL: Sequential Command Execution

- **Run `dotnet` commands that touch build state (`build`, `test`, `publish`, `format`, `restore`) and the scripts that wrap them one at a time per solution** — start the next only after the previous has exited, and never launch them in parallel background shells
- MSBuild does not coordinate concurrent processes over the same `obj`/`bin` directories, so concurrent runs contend for each other's outputs: on Windows they fail with file-lock errors (CS2012, CS0006, MSB3061); on macOS they can hang indefinitely
- A hang is indistinguishable from a slow build from the outside — the processes stay alive and emit nothing, so waiting longer never resolves it. Kill the stuck processes and rerun sequentially
