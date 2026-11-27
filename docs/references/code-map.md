---
status: active
created: 2026-11-27
---

# QueryToCsv Code Map

Feature folders and homes of cross-cutting mechanisms in QueryToCsv (`docs/rules/documentation.md`, Code Map; `docs/rules/standard.md`, One Home per Piece of Knowledge). Keep this map current in the same change whenever a feature folder or shared mechanism home is added, moved, or removed.

| Path | Knowledge |
|---|---|
| `QueryToCsv/` | The whole application in one flat folder: exporting a SQL Server result set to CSV. `Program.cs` is the entry point that selects the CLI mode (help, version, `--guide`, `--open`, one-liner, interactive), wires Ctrl+C to cancellation, and records the run's start and end |
| `QueryToCsv/CliInvocation.cs`, `QueryToCsv/CliMode.cs`, `QueryToCsv/CliRunArgs.cs` | Command-line parsing: top-level mode selection and `--open` targets, and the one-liner options |
| `QueryToCsv/QueryRun.cs` | The interactive and one-liner runs: connection and query selection, `-f` path containment, credential resolution, and the one place a failure that ends a run is reported on standard error and recorded in the log |
| `QueryToCsv/RunFailureException.cs` | A failure the operator can act on, thrown where it is detected and reported by `QueryRun` |
| `QueryToCsv/AppSettings.cs`, `QueryToCsv/ConnectionEntry.cs`, `QueryToCsv/CsvSettings.cs` | Configuration loading: reading `appsettings.json`, resolving relative folders against the executable's directory, substituting defaults for unusable values, and validating the connections |
| `QueryToCsv/CredentialStore.cs` | Reading SQL Server credentials from Windows Credential Manager |
| `QueryToCsv/appsettings.template.json` | The configuration contract: every setting the application reads, with placeholder values; the build ships it as the initial `appsettings.json` |
| `QueryToCsv/LogSetup.cs` | Logging: NLog configuration, log-directory selection with its fallback, and the retention sweep |
| `QueryToCsv/ConsoleMessages.cs` | Diagnostics on standard error in the `QueryToCsv: <message>` form, including the `--help` hint after a usage error |
| `QueryToCsv/UsageGuide.cs`, `docs/guides/usage.md` | The usage guide: the operator documentation, embedded in the executable by `QueryToCsv.csproj` and printed by `--guide` without its front matter |
| `QueryToCsv/ApplicationVersion.cs` | The application name and the version display text |
| `QueryToCsv/ConsoleUi.cs` | CSV encoding-name resolution (strict encodings) shared by the one-liner and interactive modes, and the interactive prompts |
| `QueryToCsv/SqlText.cs` | Reading SQL text: size limits, byte order marks, and strict decoding of `.sql` files |
| `QueryToCsv/QueryExecutor.cs` | Database access: the SELECT-only check on the T-SQL parse tree, and running the query into a CSV file |
| `QueryToCsv/CsvExport.cs`, `QueryToCsv/CsvValueFormat.cs` | CSV writing: quoting, column-type checks, encodability, and the text form of each value type including formula neutralization |
| `QueryToCsv/OutputFile.cs` | Output files: names, staging under the temporary folder, atomic placement in `OutputFolder`, and cleanup of leftovers |
| `QueryToCsv.TestSupport/` | Test infrastructure shared by both test projects: scratch directories (`TempDirectory`), the repository root (`Repository`), and the test-run SQL Server container with its pinned certificate (`SqlServerFixture`) |
| `QueryToCsv.Tests/ConsoleStreamsDefinition.cs` | The test collection for tests that replace the process-wide standard input and error streams, run one at a time |
| `QueryToCsv.Tests/SqlServerDefinition.cs` | The boundary-test collection sharing one SQL Server, and the `Boundary` test category |
| `QueryToCsv.Tests/TestSetup.cs` | Test-assembly initialization: the startup settings the application makes (code-page encodings, managed SqlClient networking) |
| `QueryToCsv.EndToEndTests/AppInstallation.cs`, `QueryToCsv.EndToEndTests/CredentialManager.cs` | End-to-end harness: the built executable in a test-owned folder launched as its own process, and a test-owned Credential Manager credential |
