# .NET Standards

Stack rule file specifying the .NET means and .NET-specific rules that satisfy the
shared standards under `docs/rules/`. Client packaging and desktop XAML/UI means live
separately in `dotnet-client.md` (when `dotnet-client.md` is present and applies).
Concrete commands with solution and project names live in each repository's `AGENTS.md`.

## Enforcement Matrix

Rules marked **Enforced** are fully checked mechanically by the project's build or
analyzer gates (violations fail the build) and need not be re-checked manually during
AUDIT. Rules marked **AUDIT** lack a complete mechanical guard — even when a partial
mechanism is named — and require verifying all unguarded aspects manually during AUDIT.
Where a repository lacks a listed mechanism, treat that entire rule as **AUDIT** until
the mechanism is added. Rows referencing conditional rule files (e.g., `configuration.md`,
`db.md`, `file-exchange.md`, `values.md`, `remote-calls.md`, `security.md`, `cloud.md`,
`web-api.md`) apply only when the referenced rule file is present in `docs/rules/` and
its `Applies when` condition holds for the application. Detailed build-script and
property notes live in the comments of the configuration and build files themselves.

| ID | Rule | Mechanism | Check |
|---|---|---|---|
| FORMAT | Formatting follows `.editorconfig` (`standard.md` Build Hygiene and Static Analysis) | `.editorconfig`; `dotnet format --verify-no-changes` | AUDIT — `dotnet format --verify-no-changes` enforces style against `.editorconfig`; maintaining a single root `.editorconfig` without per-file deviations and running the formatter before completion stay AUDIT |
| ANALYZERS | Recommended static analysis, warnings as errors, narrow suppressions, banned-API list, and dependency vulnerability auditing (`standard.md` Build Hygiene and Static Analysis) | `Directory.Build.props` (`AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, `TreatWarningsAsErrors`, `NuGetAudit`, `Microsoft.CodeAnalysis.BannedApiAnalyzers` + `BannedSymbols.txt`) | AUDIT — gates enforce enabled diagnostics, `NuGetAudit` advisory checks, and `BannedSymbols.txt` (`RS0030`); keeping gates enabled, suppression scopes/reasons, and keeping `RS0030` unsuppressed stay AUDIT |
| UNUSED | Remove unreachable members, parameters, and locals (`standard.md` Leave No Vestiges) | `.editorconfig` (`IDE0051`, `IDE0052`, `IDE0060` as warning + `TreatWarningsAsErrors`); compiler `CS0168`/`CS0219` | AUDIT — compiler/analyzers catch dead locals/private members/unused parameters; dead branches and stale names stay AUDIT |
| NAMESPACE | File-scoped namespaces and namespace matches folder (`standard.md` Code Organization) | `.editorconfig` (`csharp_style_namespace_declarations=file_scoped:warning`, `IDE0130` as warning); `Directory.Build.props` (`EnforceCodeStyleInBuild`, `TreatWarningsAsErrors`) | AUDIT — analyzers enforce file-scoped syntax (`IDE0161`) and declared-namespace folder alignment (`IDE0130`) when build code-style enforcement is enabled; files omitting a `namespace` declaration, multi-file `partial` types, and `<RootNamespace>` alignment stay AUDIT |
| ORG | One top-level type per `.cs` file matching filename (`standard.md` Code Organization) | — | AUDIT |
| ASYNC | Use async BCL I/O APIs; no blocking on async code; no `async` without `await` (`standard.md` Synchronous vs Asynchronous) | `Directory.Build.props` (`Microsoft.VisualStudio.Threading.Analyzers`) | AUDIT — VSTHRD enforces blocking (`VSTHRD002`) and `async void` (`VSTHRD100`); choosing async BCL I/O APIs, avoiding `async` without `await` or with pre-completed tasks, and validating any `VSTHRD100`/`VSTHRD002` suppression stay AUDIT |
| DEFERRED | Enumerate LINQ queries once; no query side effects; catch at enumeration | `.editorconfig` (`CA1851` as warning + `TreatWarningsAsErrors`) | AUDIT — `CA1851` enforces multiple enumeration; query side effects and `try` placement stay AUDIT |
| VISIBILITY | Minimal visibility and explicit modifiers (`standard.md` Minimal Visibility) | `.editorconfig` (`CA1515`, `IDE0040` as warning + `TreatWarningsAsErrors`); `<InternalsVisibleTo>` in application `.csproj` | AUDIT — `CA1515`/`IDE0040` flag `public` types in exe projects and omitted modifiers; `public` members on `internal` types, `BindNonPublicProperties`, `InternalsVisibleTo` scope, and test method modifiers stay AUDIT |
| VAR | Allow `var` only on explicit-type `new` expressions and explicit casts; explicit types are always permitted | `.editorconfig` suggestions (`csharp_style_var_elsewhere`, `csharp_style_var_for_built_in_types`) | AUDIT — suggestions do not fail the build; verifying no opaque `var` remains stays AUDIT |
| STRING | Explicit `StringComparison` on string methods | — | AUDIT |
| CANCEL | Propagate an optional `CancellationToken` parameter (`= default`) through async I/O (`standard.md` Synchronous vs Asynchronous) | — | AUDIT |
| CLOCK | Pass the current time as an argument; `TimeProvider` only where an argument cannot carry it (`standard.md` Functional Core, Imperative Shell) | — | AUDIT |
| FILEPATH | Anchor application paths to `AppContext.BaseDirectory`, `Environment.GetFolderPath(...)`, a configured absolute path, or `Path.GetTempPath()` for transient temporary files; avoid hash- or version-keyed settings providers (`standard.md` Application-Owned Paths) | `Directory.Build.props` + `BannedSymbols.txt` banning `Directory.GetCurrentDirectory` and `Environment.CurrentDirectory` (`RS0030`) | AUDIT — `BannedSymbols.txt` enforces those two APIs; bare relative paths and provider-generated persistent paths stay AUDIT |
| DESERIALIZE | Concrete-type deserialization via `System.Text.Json`; forbid `BinaryFormatter` and polymorphic type handling (`standard.md` Values Never Become Code) | — | AUDIT |
| CONSTANTS | Application constants as `const` / `static readonly` in owning class (`standard.md` Named Constants) | — | AUDIT |
| COMMENTS | Inline `//` comments; XML docs only on published libraries (`standard.md` Code Comments) | — | AUDIT |
| TESTNAME | xUnit test naming `Method_Scenario_ExpectedResult` | — | AUDIT |
| REQCOV | Attach `[Trait("Requirement", "<ID>")]` or `// <ID>` comment to verifying tests (`testing.md` Requirement Coverage) | — | AUDIT |
| VERSION | Single `<Version>` in `Directory.Build.props` and metadata-based version display (`standard.md` Version Display) | `Directory.Build.props` | AUDIT |
| OUTPUT | Distributables produced only via build script; build outputs untracked (`standard.md` Build Hygiene and Static Analysis) | `.gitignore` | AUDIT — `.gitignore` does not stop forced adds or manual `dotnet publish` |
| CONFIGFILE | Track placeholder template only; never track active config file (`configuration.md`, when present and applicable) | `.gitignore` | AUDIT — `.gitignore` does not stop forced adds of the active file |
| SERIAL | Run build-state `dotnet` commands one at a time per solution | — | AUDIT |
| DB | .NET data access means (`db.md`, when present and applicable) | — | AUDIT |
| FILEEX | .NET file-exchange and temp-staging means (`file-exchange.md`, when present and applicable) | — | AUDIT |
| VALUES | .NET decimal, rounding, invariant formatting, and date/time types (`values.md`, when present and applicable) | — | AUDIT |
| HTTP | .NET `HttpClient` reuse and timeout means (`remote-calls.md`, when present and applicable) | — | AUDIT |
| SECURITY | .NET cryptographic RNG (`security.md`, when present and applicable) | — | AUDIT |
| CLOUD | .NET SDK credential chains (`DefaultAzureCredential`) and scoped `BlobSasBuilder` / `GetPreSignedUrlRequest` builders (`cloud.md`, when present and applicable) | — | AUDIT |
| WEBAPI | ASP.NET Core `AddProblemDetails`/`UseExceptionHandler`, `KestrelServerLimits`/request-size limits, and `AddRateLimiter` (`web-api.md`, when present and applicable) | — | AUDIT |

---

## FORMAT: Code Formatting

- **Single root configuration**: maintain style settings solely in `.editorconfig` at the repository root without per-file formatting deviations.
- **Pass check mode before completion**: run the repository's formatter command and ensure `--verify-no-changes` exits 0 (`AGENTS.md` Commands).

## ANALYZERS: Static Analysis

- **Keep gates enabled**: never disable or weaken `AnalysisLevel`, `EnforceCodeStyleInBuild`, or `TreatWarningsAsErrors` in `Directory.Build.props`.
- **Suppress only at the narrowest permitted scope**: tolerate a specific diagnostic solely through the narrowest scope below, stating a current constraint reason (never "for now" or "fix later"):

| Scope | Mechanism |
|---|---|
| Single line or statement | `#pragma warning disable <ID>` paired with `restore` and an inline reason comment |
| Single declaration (type or member) | `[SuppressMessage("<Category>", "<ID>", Justification = "<reason>")]` on the declaration |
| Generated file or folder | `.editorconfig` glob matching one project only with `dotnet_diagnostic.<ID>.severity = none` and a reason comment |
| Whole project | `<NoWarn>` in that project's `.csproj` with a reason comment |

- **Match suppression scope to the diagnostic target**: use `#pragma warning disable`/`restore` for diagnostics raised on a single statement or expression, and use `[SuppressMessage]` for diagnostics raised on a whole type or member declaration so the suppression travels with refactored declarations; reserve file/folder `.editorconfig` rules for generated code overwritten on regeneration.
- **Keep NuGet vulnerability auditing on**: keep `NuGetAudit` enabled so packages with known vulnerabilities are reported at restore (`standard.md` Build Hygiene and Static Analysis — Known-vulnerable dependencies are found).
- **No repository-wide suppression or exempted banned APIs**: never lower a diagnostic globally across multiple projects, and never suppress `RS0030` — remove any API from `BannedSymbols.txt` instead if any code calls it legitimately (`standard.md` Build Hygiene and Static Analysis).

## UNUSED: Unreachable Code

- **Delete compiler- and analyzer-reported dead symbols**: remove locals (`CS0168`/`CS0219`), private members (`IDE0051`/`IDE0052`), and unused parameters (`IDE0060`); update callers rather than keeping unused parameters.
- **External-signature exemption**: where an interface, delegate, or framework callback fixes a parameter list, suppress `IDE0060` at the narrowest scope naming the fixing contract.

## NAMESPACE: Namespaces and Folder Alignment

- **File-scoped namespaces only**: write `namespace X;` (`csharp_style_namespace_declarations=file_scoped:warning`), following the modern C# convention to eliminate one redundant indentation level across every source file and keep code and diffs readable.
- **Match folder hierarchy**: align every namespace with its folder path from the project root (`IDE0130` as warning) to satisfy `standard.md` (Code Organization).

## ORG: Source File Organization

- **One top-level type per `.cs` file**: name the file `<TypeName>.cs` matching its top-level type (`standard.md` Code Organization), except framework-paired partial files (e.g., code-behind files paired with markup).

## ASYNC: Asynchronous Code

Refines `standard.md` (Synchronous vs Asynchronous) for .NET:

- **Use async BCL I/O APIs**: invoke asynchronous file, network, and database APIs (e.g., `File.ReadAllTextAsync`) instead of synchronous equivalents.
- **Never block on tasks**: never call `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` (enforced by `VSTHRD002`) — never suppress `VSTHRD002` on UI dispatcher threads or general code; only when a framework contract mandates a synchronous non-`void` signature with no async alternative and no synchronization-context deadlock can occur may `VSTHRD002` be suppressed at the narrowest `ANALYZERS` scope naming the contract.
- **Return completed tasks synchronously without `async`**: when implementing a `Task` or `Task<T>` signature synchronously, return `Task.CompletedTask` or `Task.FromResult(...)` directly without the `async` modifier, and never `await` pre-completed tasks.
- **Framework `void` event handlers**: where a framework event delegate mandates a `void` signature (e.g., UI click handlers), `async void` is permitted with a narrowest-scope `VSTHRD100` suppression (`ANALYZERS`) naming the event, provided the handler catches and handles exceptions internally (`standard.md` Failure Handling and Reporting) so unhandled exceptions cannot crash the process.

## DEFERRED: Deferred Execution (LINQ)

Deferred LINQ queries execute upon enumeration rather than at query construction, so side effects run unpredictably or repeatedly and execution-time exceptions bypass `try` blocks around query construction:

- **No side effects inside queries**: keep `Select`, `Where`, and deferred LINQ operators free of mutations, I/O, or logging; perform side effects inside `foreach` / `await foreach` (over a materialized collection, or over a streamed `DbDataReader` / `IAsyncEnumerable<T>` when streaming large results per `DB` and `db.md` when present and applies).
- **Materialize before re-enumerating**: call `.ToList()` or `.ToArray()` once when converting a deferred `IEnumerable<T>` into data (`CA1851`).
- **Catch exceptions at enumeration**: place `try`/`catch` around the enumeration or `.ToList()`/`.ToArray()` call, not solely around query construction.

## VISIBILITY: Accessibility Modifiers

Implements `standard.md` (Minimal Visibility) in C#:

- **Keep application types `internal`**: declare types in `Exe`/`WinExe` projects `internal` (enforced by `CA1515`; client XAML entry-point exceptions live in `dotnet-client.md` when `dotnet-client.md` is present and applies).
- **Always state explicit modifiers**: write explicit accessibility modifiers on every declaration except interface members (`IDE0040`), ensuring visibility is always a deliberate, reviewable choice rather than an implicit C# default.
- **Keep members on `internal` types `internal` or narrower**: use `public` inside an `internal` type only when a framework, serializer, UI binder, code generator, or C# language rule requires it (e.g., the callers below), and never enable options such as `BinderOptions.BindNonPublicProperties` to bypass default visibility:

| Caller | Requires `public` |
|---|---|
| C# language | Implicit interface implementations and overrides of `public` members |
| Serializers (e.g., `System.Text.Json`) | Bound DTO properties when avoiding private-member serialization attributes |
| `ConfigurationBinder` | Bound configuration properties (`public` getter/setter or `init`) |
| `Microsoft.Extensions.DependencyInjection` | Constructor used to resolve registered services |

- **Grant test access via `InternalsVisibleTo`**: add `<InternalsVisibleTo Include="<TestAssembly>" />` (e.g., `<App>.Tests`) in the application `.csproj` for the application's test assemblies instead of making application types `public`.
- **xUnit visibility**: declare test classes `public` (required by `xUnit1000`) and test methods `internal` so `[Theory]` methods can accept `internal` application types without `CS0051` or `IDE0051`.

## VAR: Type Inference

- **Allow `var` only when the exact type is named on the right-hand side**: use `var` only for explicit-type `new` expressions (e.g., `new T(...)`, `new T[...]`) and explicit casts (`(T)expr`), and write an explicit type for every other initializer (e.g., method calls, property reads, literals) so readers outside an IDE (e.g., during code review) do not misread types such as `Task<T>`, deferred sequences, or numeric widths; explicit type declarations (including target-typed `new()`) are always permitted.

## STRING: String Comparison

- **Always pass `StringComparison`**: supply `StringComparison` explicitly to `StartsWith`, `EndsWith`, `IndexOf`, `Contains`, and `Equals` because BCL string methods have inconsistent defaults (`StartsWith`, `EndsWith`, and `IndexOf` default to `CurrentCulture` while `Contains` and `Equals` default to `Ordinal`), causing silent locale-dependent bugs across machines — use `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase` for technical identifiers and paths, and pass the specification-defined culture-aware `StringComparison` explicitly when linguistic user-facing matching is required (`values.md`, when present).

## CANCEL: Cancellation Token Propagation

Implements `standard.md` (Synchronous vs Asynchronous) cancellation propagation:

- **Accept and forward a defaulted `CancellationToken` parameter**: include an optional `CancellationToken` parameter with `= default` (e.g., `CancellationToken cancellationToken = default` or `CancellationToken ct = default`) on every async I/O method whose signature the application controls (including internal helpers and static utilities) even when current callers pass no token, and forward that token (never `CancellationToken.None`) to every cancellable await inside it — retrofitting tokens later requires cross-layer signature changes across helpers and all their callers.
- **Permitted omissions**: omit the parameter solely on (1) the non-cancellable completion steps defined in `standard.md` (Synchronous vs Asynchronous) or (2) methods whose signature is fixed by an external interface, delegate, or framework callback (e.g., `async void` event handlers under `ASYNC`), forwarding any cancellation token supplied by the enclosing lifetime or framework context when available.

## CLOCK: Current Time

Implements `standard.md` (Functional Core, Imperative Shell — Pass ambient values as inputs) in .NET:

- **Read the clock in the shell**: read `DateTimeOffset.UtcNow` (or `TimeProvider.GetUtcNow()`) in the I/O shell and pass the value to decision code as an argument; where a single argument cannot carry the changing time (e.g., measuring elapsed duration, timers), inject `TimeProvider` rather than a custom clock abstraction.

## FILEPATH: File Paths

Implements `standard.md` (Application-Owned Paths) in .NET:

- **Anchor application paths in code**: construct application-owned paths solely from `AppContext.BaseDirectory`, `Environment.GetFolderPath(...)`, or a configured absolute path (handling relative configured paths per `standard.md` Application-Owned Paths; `configuration.md`, when present, or test configuration under `testing.md`) — and use `Path.GetTempPath()` solely for transient per-operation temporary files (e.g., `FILEEX` network staging when `file-exchange.md` applies, atomic-write temporary files, or isolated test scratch directories) — never from `Directory.GetCurrentDirectory()`, `Environment.CurrentDirectory` (blocked by `BannedSymbols.txt`), or bare relative strings. (User-supplied relative CLI arguments resolve intentionally against the caller's working directory.)
- **Avoid hash- or version-keyed settings providers**: do not store persistent settings or data through `ApplicationSettingsBase` / `Properties.Settings` (`LocalFileSettingsProvider`), which writes `user.config` into URL- or version-hashed directories; serialize persistent files explicitly (e.g., via `System.Text.Json`) at the specification-defined path anchored per `Anchor application paths in code` above.

## DESERIALIZE: Safe Deserialization

Implements `standard.md` (Values Never Become Code — Forbid type-polymorphic deserialization of untrusted data) in .NET:

- **Use concrete-type deserialization only**: deserialize untrusted JSON payloads solely via `System.Text.Json` into explicit static DTO types; across all formats, never use `BinaryFormatter`, `TypeNameHandling.All/Auto`, or other type-polymorphic deserializers.

## CONSTANTS: Constants

Implements `standard.md` (Named Constants) in C#:

- **Define constants on owning types**: declare compile-time constants as `const` (or `static readonly` for non-primitive types) inside the owning class rather than catch-all constant bags.

## COMMENTS: Code Comments

Implements `standard.md` (Code Comments) in C#:

- **Inline comments vs XML docs**: use `//` inline comments and reserve XML documentation comments (`/// <summary>`) strictly for libraries published to callers outside the repository, because XML doc comments exist to generate external API documentation and writing them on internal application members encourages boilerplate restatement comments that violate `standard.md` (Code Comments) and drift stale.

## TESTNAME: xUnit Test Naming

- **Name pattern**: name test methods `<Method>_<Scenario>_ExpectedResult` (e.g., `Merge_EmptyInput_ReturnsEmpty`) — following the official .NET unit-testing convention so test failure logs show the target, condition, and expected outcome at a glance — and suppress `CA1707` in the test project's `.csproj` `<NoWarn>` with that reason comment (`ANALYZERS`).

## REQCOV: Requirement Coverage

Implements `testing.md` (Requirement Coverage) in .NET:

- **Tag tests with `[Trait("Requirement", "<ID>")]`**: attach `[Trait("Requirement", "<ID>")]` to each verifying test method (supporting `dotnet test --filter "Requirement=<ID>"`), or place a `// <ID>` comment directly above the method or its first attribute when an attribute cannot be used (e.g., shared source outside test methods).

## VERSION: Version Management

Implements `standard.md` (Version Display) in .NET:

- **Define `<Version>` solely in `Directory.Build.props`**: keep `<Version>` out of individual `.csproj` files and format it as an MSBuild-compatible numeric version string (e.g., `Major.Minor.Patch`).
- **Read displayed version from assembly metadata**: read `AssemblyInformationalVersionAttribute` (derived from `<Version>`) to obtain the build's version at run time.

## OUTPUT: Build Outputs

- **No manual distributables or tracked outputs**: invoke the gated build script (`AGENTS.md` Commands) rather than running `dotnet publish` directly to produce distributables, and never force-add or place manual files into gitignored build output directories.

## CONFIGFILE: Configuration Files

Applies when `configuration.md` is present and applies (`configuration.md` Configuration Files in the Repository):

- **Track `<name>.template.<ext>` (e.g., `appsettings.template.json`)**: keep the runtime file (e.g., `appsettings.json`) untracked in `.gitignore` instead of tracking `appsettings.json` directly, because .NET developers routinely edit `appsettings.json` locally and separating the tracked template filename stops local edits from entering commits.

## SERIAL: Sequential Command Execution

- **Never run build-state `dotnet` commands concurrently**: execute `dotnet build`, `test`, `publish`, `format`, `restore`, and their wrapper scripts sequentially per solution — never in parallel shells — because MSBuild does not coordinate concurrent access to `obj`/`bin` and fails with file locks on Windows or hangs indefinitely on macOS.

---

## DB: Database Means

Applies when `db.md` is present and applies:

- **Avoid implicit parameter typing (`AddWithValue`)**: never use `AddWithValue` on ADO.NET parameters; declare explicit column types and sizes (e.g., `SqlDbType` and `Size` / `Precision` / `Scale`, or `DbType` on generic providers).
- **Handle parameter limits cleanly**: pass variable-length sets via table-valued parameters (TVPs), `SqlBulkCopy` / provider bulk APIs, or named constant-sized chunks, grouping chunked writes per `db.md` (`Query Shape and Parameters` and `Transactions, Concurrency, and Batching`).
- **Stream large results**: iterate large queries with `DbDataReader` or `IAsyncEnumerable<T>` rather than filling full in-memory collections (`db.md` Query Shape and Parameters).

## FILEEX: File Exchange and Staging Means

Applies when `file-exchange.md` is present and applies:

- **Canonical path containment check**: normalize the base directory via `Path.GetFullPath(...)` (ensuring a single trailing `Path.DirectorySeparatorChar`) and the candidate path via `Path.GetFullPath(...)`, comparing with `StartsWith(..., StringComparison.OrdinalIgnoreCase)` on Windows (or `Ordinal` on case-sensitive filesystems).
- **Explicit strict encodings**: instantiate explicit `Encoding` objects configured to throw on invalid bytes (e.g., `new UTF8Encoding(encoderShouldEmitUTF8Identifier: ..., throwOnInvalidBytes: true)`) on `StreamReader` / `StreamWriter` / `File` calls rather than omitting encoding.
- **Stage network file transfers under `Path.GetTempPath()`**: create each operation's temporary staging files under an application-owned subdirectory or naming prefix inside `Path.GetTempPath()` (e.g., `{AppName}/...` or `{AppName}_...`), and delete the temporary files created by that operation in a `finally` block (cleanup error handling and crash-orphan recovery follow `file-exchange.md` Network-Path Staging and Temporary Files).

## VALUES: Precision, Invariant Formatting, and Temporal Means

Applies when `values.md` is present and applies:

- **Exact numeric types and rounding**: use `decimal` (or integer minor units) for money and exact quantities — never `double` or `float` — and pass `MidpointRounding` explicitly to every `Math.Round` or `decimal.Round` call.
- **Culture-invariant machine formatting**: pass `CultureInfo.InvariantCulture` (or a caller-supplied invariant `IFormatProvider`) explicitly to every machine-facing `Parse`, `TryParse`, and `ToString` call.
- **Explicit temporal types**: use `DateOnly` for calendar dates without time of day, and `DateTimeOffset` or UTC `DateTime` (`DateTimeKind.Utc`) for instants.

## HTTP: Outbound HTTP Means

Applies when `remote-calls.md` is present and applies:

- **Reuse pooled clients**: obtain `HttpClient` from `IHttpClientFactory` or a shared long-lived client configured with `SocketsHttpHandler.PooledConnectionLifetime` — never `new HttpClient()` per call.
- **Explicit connection and request timeouts**: configure `SocketsHttpHandler.ConnectTimeout` (not left at `Timeout.InfiniteTimeSpan`), `HttpClient.Timeout`, and per-call `CancellationTokenSource` deadlines explicitly on all outbound HTTP calls.

## SECURITY: Security Means

Applies when `security.md` is present and applies:

- **Cryptographic randomness**: generate tokens and unpredictable IDs with `System.Security.Cryptography.RandomNumberGenerator`, never `System.Random`.

## CLOUD: Cloud Storage and Identity Means

Applies when `cloud.md` is present and applies (implements `cloud.md` Short-Lived and Managed Cloud Identities and Short-Lived Signed Transfer URLs):

- **Use `DefaultAzureCredential` or provider workload-identity chains**: initialize cloud SDK clients with `DefaultAzureCredential` (or AWS `FallbackCredentialsFactory` / GCP `GoogleCredential.GetApplicationDefaultAsync`), avoiding static account keys, connection-string secrets, or `StorageSharedKeyCredential` in application code or plaintext configuration (except where `cloud.md` permits a secret-store-backed static key for an external integration constraint).
- **Scope SAS and pre-signed URL builders cleanly**: construct direct-transfer URLs with `BlobSasBuilder` (signed with `UserDelegationKey` rather than shared key, narrow `BlobSasPermissions`, short `ExpiresOn`) or AWS `GetPreSignedUrlRequest` (narrow `HttpVerb` and `Expires`), avoiding `BlobContainerSasBuilder` container-wide tokens.

## WEBAPI: Web API Means

Applies when `web-api.md` is present and applies (implements `web-api.md` Uniform Error Format and Status Codes and Request Size and Rate Limits):

- **Wire ASP.NET Core `ProblemDetails` middleware**: register `AddProblemDetails`, `UseExceptionHandler`, `UseStatusCodePages`, and custom `IExceptionHandler` mappings in the pipeline rather than ad-hoc error envelopes or `UseDeveloperExceptionPage` in deployed builds.
- **Configure Kestrel/endpoint limits and `AddRateLimiter`**: set explicit bounds via `KestrelServerLimits` (`MaxRequestBodySize`, `MaxRequestHeadersTotalSize`, `MaxRequestLineSize`), `[RequestSizeLimit]`, and ASP.NET Core `AddRateLimiter` / `UseRateLimiter` policies on exposed endpoints.
