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
| DEFERRED | A LINQ query is deferred: no side effects inside a query, enumerate once, exceptions surface at enumeration | `.editorconfig` sets `dotnet_diagnostic.CA1851.severity = warning`, which `TreatWarningsAsErrors` turns into a build error | Enforced for multiple enumeration — side effects inside a query and the placement of `try` have no analyzer and stay AUDIT |
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
| CONFIGFILE | Only the placeholder template of a configuration file is tracked; the file the application reads is never committed | `Build.ps1` derives the real name of every tracked `*.template.*` configuration file and fails when git tracks it; the scaffold gitignores `appsettings.json` | Enforced for the commit — deriving the shipped file and the installer's no-overwrite entry stay AUDIT items |
| NATIVEDEP | The distributable is as few files as possible, carries no debug symbols, and never unpacks itself at run time; native dependencies are reduced to reach that | `Directory.Build.props` fails the build when `IncludeNativeLibrariesForSelfExtract` is enabled and drops every `.pdb` from the publish list; the installer script ships the whole publish output | Enforced for the property and the symbols — how far the native dependencies are reduced, and keeping the installer's file list exhaustive, stay AUDIT items |
| RUNNINGAPP | Installing over, and uninstalling, a running application is handled: the processes are detected, the user is offered a way to have them closed, and nothing is force-closed on the user's behalf | The installer script sets `CloseApplications=yes` for the update path and drives the Restart Manager from `InitializeUninstall` for the uninstall path | AUDIT — the scaffold ships the mechanism, but nothing fails when an installer script drops it |
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

## DEFERRED: Deferred Execution (LINQ)

A LINQ query is a description of work, not the work. `foreach` runs; a query waits for
something to enumerate it. Three failures come out of that gap, and none of them
announces itself.

- **Never place a side effect inside a query.** `Select`, `Where`, and the rest run when the sequence is enumerated — later than the line that reads as the moment it happens, possibly more than once, possibly never. A save, a log write, or an assignment inside a projection fires at a time nothing in the code shows. Transform in the query; act in a `foreach` over the materialized result
- **Enumerate once.** A sequence still typed as `IEnumerable<T>` re-runs its whole query on every enumeration, and a query over a database or the file system re-runs that I/O with it. Materialize with `ToList()` or `ToArray()` at the point the sequence stops being a query and becomes data. CA1851 makes a second enumeration a build error
- **An exception surfaces where the sequence is enumerated, not where the query was written.** A `try` wrapped around the query definition catches nothing at all. Wrap the enumeration, or materialize inside the `try`
- Materializing is the same act `standard.md` Snapshot Before Mutate requires before modifying the source being iterated: one deferral, seen from the mutation side

## NAMESPACE: Namespaces

- **File-scoped namespaces only** (`namespace X;`) — enforced via `.editorconfig` + warnings-as-errors

## VAR: Type Inference

- **Use `var`** for `new` expressions and other obvious types
- **Use explicit types** when the type is not clear from the right-hand side

## STRING: String Comparison

A comparison written without `StringComparison` takes whatever the platform default is,
and leaves the reader — and every analyzer and reviewer that passes over it — to work out
which semantics were meant. Naming it settles that where the code is written.

- **Always specify `StringComparison`** for string methods (`StartsWith`, `EndsWith`, `IndexOf`, `Contains`, `Equals`)
- **Use `StringComparison.Ordinal`** for technical comparisons (paths, identifiers)
- **Use `StringComparison.OrdinalIgnoreCase`** when case-insensitivity is needed

## ERROR: Error Handling

- **No empty catch blocks:** Every `catch` must handle or log the exception
- **No raw exception messages in output:** Never display `ex.Message` directly to users — it is written for a developer, and carries internal paths, connection strings, and type names
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

Reading and writing straight against a network path fails in ways local I/O does not:
every operation carries the link's latency, and a connection dropped mid-write leaves a
truncated file at the destination. Doing the work locally and moving the finished file
keeps both problems away from the destination.

When I/O targets may be network paths, perform all file reads/writes in local `%TEMP%`.

- **Input**: Copy source files from network to `%TEMP%` before processing
- **Output**: Create artifacts in `%TEMP%`, then `File.Copy` / `File.Move` to the final destination
- **Naming**: Flat under `Path.GetTempPath()` as `{FeatureName}_{Guid.NewGuid():N}.{ext}`
- **Cleanup**: Always delete in `finally`. Deletion failure is best-effort (log and continue)
- **Recleanup of leftover files**: Don't rely on the `finally` deletion alone — a killed process or crash can skip it, so retry cleanup elsewhere too
  - **Default policy: delete leftover files older than 24 hours.** An application whose run interval makes 24 the wrong number picks its own limit and states the value and the reason in its application rules file
  - **Desktop apps**: On next startup, retry deleting leftover temp files past that age
  - **Console/long-running apps**: On startup, or at a suitable point within the processing loop (start, end, etc. — whatever fits the app's structure), retry the same way

## CONSTANTS: Constants

- **Name a value when the use site cannot show what it means, or when several sites must keep holding the same value.** A literal whose meaning is evident where it is written (`0`, `1`, an empty string) stays a literal — extracting it names nothing
- **Application constants** as `const` in the owning class
- A value operators adjust in the field is a configuration value, not a constant — Configuration Values in `standard.md` states what making it configurable obliges

## COMMENTS: Code Comments (C# specifics)

The comment philosophy (when to write, what never to write) is in `standard.md`.
C# specifics:

- **Language:** English
- **Style:** Simple inline comments (`//`). XML documentation comments (`<summary>`, ...) describe an API published to callers outside the repository; an application that publishes none does not write them. A project that ships a library for others to reference is where they belong

## TESTNAME: xUnit Test Naming

- Test method names use `Method_Scenario_ExpectedResult` (e.g. `Merge_EmptyInput_ReturnsEmpty`)
- Underscore separation is the official convention; for this reason CA1707 is suppressed via `<NoWarn>` in the test project's csproj
- Test row ordering and the other testing principles are in the `standard.md` Testing section

## VERSION: Version Management

- **The version is defined ONLY in the `<Version>` tag of `Directory.Build.props`.** Never add `<Version>` to a csproj or `#define MyAppVersion` to the installer script — duplicate definitions are how versions drift out of sync. The EXE inherits it via MSBuild; the installer build injects it (the `.iss` fails with `#error` when it is not injected)
- **Version bump**: update `<Version>`, add a `## [x.y.z]` heading and entry to `CHANGELOG.md` if it exists, and include both in the **same commit**. Building the installer without the changelog heading fails at the gate
- **The installer build is the release packaging step** — it carries the CHANGELOG gate; the EXE that `Build.ps1` publishes is a build output the installer wraps, not a release
- Semantic Versioning (MAJOR.MINOR.PATCH); during development `0.x.x` (release as `1.0.0`); MAJOR: incompatible changes, MINOR: new features, PATCH: bug fixes and small changes
- **Displayed version** (Version Display in `standard.md`, VERSIONOUT in `cli.md`): read `AssemblyInformationalVersionAttribute` — it equals `<Version>` because `Directory.Build.props` sets `IncludeSourceRevisionInInformationalVersion=false`, which stops the SDK from appending `+<git sha>`. Never remove that property while the displayed version must stay `<AppName> X.Y.Z`

## OUTPUT: Build Outputs

The build script is where the format check and the test suite run. An artifact that
reached the output directory by any other route never passed them, and nothing about the
file says so.

- **Never manually add files to build output directories, and never commit them** — outputs are produced only by the build scripts (the scaffold gitignores the output directories; `AGENTS.md` names them)
- **Produce distributables only via the build scripts** — `Build.ps1` runs the format check and the test suite before publishing; invoking `dotnet publish` by hand skips those gates

## CONFIGFILE: Configuration Files

The rule is Configuration Files in the Repository in `standard.md`. Its .NET form:

- **The tracked template is `appsettings.template.json`**; the file the application reads is `appsettings.json`, which the scaffold gitignores. Any other shipped configuration file follows the same `<name>.template.<ext>` naming
- **`Build.ps1` fails when git tracks the real name** derived from a tracked `*.template.*` file with a configuration extension. The check needs no list to maintain and stays inert in a repository that has no template file
- **`Build.ps1` copies the template into the publish output under the real name** — that copy, not any file from the working tree, is what the installer packages
- **The installer entry for a configuration file carries `onlyifdoesntexist`** and is excluded from the wildcard that ships the rest of the publish output, so an update keeps the operator's settings

## NATIVEDEP: Native Dependencies and Publish Layout

**The goal is the smallest distributable: one self-contained executable.** Self-contained
single-file publishing reaches it by bundling the runtime and every managed assembly into
the executable — that is what `PublishSingleFile` in each application's csproj is for, and
a project with no native dependency publishes the application itself as a single
executable.

NuGet-provided native libraries are the one thing that cannot go inside. Bundling them is
possible — `IncludeNativeLibrariesForSelfExtract` does it — but they are then unpacked at
run time into a per-build directory under the user's temp folder, one directory per build,
and the runtime deletes none of them: an artifact that grows without bound on a machine
nobody is watching, the same failure the LOGGING retention rule exists to prevent. One
fewer file to download is not worth that.

So the single executable is earned by **not depending on native libraries**, never by
hiding them inside the bundle.

- **Keep native dependencies out.** Before taking a NuGet package, check whether it carries native assets: a package that does costs the application its single-file shape for as long as it stays
- **Remove the ones already there.** A native library the application never reaches is still published — drop it with `ExcludeAssets="all"` on a direct `PackageReference` to the transitive package. Where the library offers a managed implementation of the same function, select it (for example an `AppContext` switch replacing a native networking layer) and exclude the native package
- **Never enable `IncludeNativeLibrariesForSelfExtract`** — `Directory.Build.props` fails the build when it is set, because nothing ever cleans up the temp directories it creates
- **A framework can switch self-extraction on by itself, and that settles the question for the whole application.** WinUI 3 does: publishing it as a single file turns on `IncludeAllContentForSelfExtract`, and the executable then unpacks its entire content set — hundreds of files — into the temp folder on every run. An application built on such a framework is not published as a single file at all; the same guard fails the build, and its message names the property that was set
- **What genuinely cannot be removed ships beside the executable.** A UI framework's rendering engine, a database engine's own binary: publishing writes them next to the executable and nothing is unpacked at run time. However many such files there are, they stay beside the executable — the line is drawn at run-time extraction, not at a file count. A native dependency set large enough to feel uncomfortable is a reason to remove more of it, never a reason to bundle it
- **The installer's file list covers the whole publish output**, never a named executable alone: a list naming only the executable installs a broken application the moment a native dependency appears
- **Debug symbols never ship.** `Directory.Build.props` removes every `.pdb` from the publish list, because the installer names nothing and would otherwise package whatever is there. `DebugType=none` reaches only this build's own symbols; a native package carries its own beside its library, and those arrive through the runtime-asset copy that no compiler switch touches. A project that genuinely must ship symbols redefines `RemoveSymbolsFromPublish` as an empty target in its csproj

## RUNNINGAPP: Installing Over and Removing a Running Application

- **The installer handles the application being open**, in both directions: an update that replaces files the running application holds, and an uninstall that deletes them. An uninstaller that ignores this leaves a half-removed installation behind — the executable stays locked on disk while the entry that would have removed it is already gone
- **The update path uses Inno Setup's own support.** `CloseApplications` (whose default is `yes`) has the Windows Restart Manager detect the processes holding files the install replaces, ask the user, close them, and start them again after the install. The installer script states it explicitly rather than leaning on the default
- **`AppMutex` is never declared.** It blocks at startup, before the Restart Manager stage is reached, so declaring it replaces "closed and restarted automatically" with "close it yourself and start over". It also applies to Setup and Uninstall together and cannot be limited to uninstall, which is the path that actually needs the help
- **The uninstall path drives the Restart Manager from `[Code]`**, because Inno Setup's close-applications support covers Setup only and the uninstaller's command line has no switch for it. `InitializeUninstall` registers every `*.exe` under the installation directory except the uninstaller itself, counts the processes using them, and offers three choices: close them and retry, retry after closing them by hand, or cancel the uninstall
- **Nothing is force-closed on the user's behalf.** `RmShutdown` is called without `RmForceShutdown`; when a graceful shutdown does not take, the choice returns to the user. Forcing a shutdown discards whatever the user has not saved
- **Only executables are registered, never libraries.** The Restart Manager reports whichever process holds a registered file, so registering a DLL would list the process that loaded it — a shell extension would drag Explorer into the shutdown
- **A silent uninstall closes the application and continues.** Unattended deployment is what runs one, and stopping there fails the deployment with nothing on screen to explain why
- **A detection that fails never blocks the uninstall.** When the Restart Manager cannot answer, the uninstall proceeds as it would have without the check — a check that could not run is not grounds for refusing to remove an application

## SERIAL: Sequential Command Execution

- **Run `dotnet` commands that touch build state (`build`, `test`, `publish`, `format`, `restore`) and the scripts that wrap them one at a time per solution** — start the next only after the previous has exited, and never launch them in parallel background shells
- MSBuild does not coordinate concurrent processes over the same `obj`/`bin` directories, so concurrent runs contend for each other's outputs: on Windows they fail with file-lock errors (CS2012, CS0006, MSB3061); on macOS they can hang indefinitely
- A hang is indistinguishable from a slow build from the outside — the processes stay alive and emit nothing, so waiting longer never resolves it. Kill the stuck processes and rerun sequentially
