---
status: active
created: 2026-11-27
---

# QueryToCsv Code Map

Feature folders and homes of cross-cutting mechanisms in QueryToCsv (`docs/rules/documentation.md`, Code Map; `docs/rules/standard.md`, One Home per Piece of Knowledge). Keep this map current in the same change whenever a feature folder or shared mechanism home is added, moved, or removed.

| Path | Knowledge |
|---|---|
| `QueryToCsv/` | The whole application in one flat folder: exporting a SQL Server result set to CSV. `Program.cs` is the entry point that selects the CLI mode (help, version, `--open`, one-liner, interactive) and records the run's start and end |
| `QueryToCsv/CliInvocation.cs`, `QueryToCsv/CliMode.cs`, `QueryToCsv/CliRunArgs.cs` | Command-line parsing: top-level mode selection and `--open` targets, and the one-liner options |
| `QueryToCsv/QueryRun.cs` | The interactive and one-liner runs, and the one place a failure that ends a run is reported on standard error and recorded in the log |
| `QueryToCsv/AppSettings.cs`, `QueryToCsv/ConnectionEntry.cs`, `QueryToCsv/CsvSettings.cs` | Configuration loading: reading `appsettings.json`, resolving relative folders against the executable's directory, substituting defaults for unusable values, and checking required input |
| `QueryToCsv/appsettings.template.json` | The configuration contract: every setting the application reads, with placeholder values; the build ships it as the initial `appsettings.json` |
| `QueryToCsv/LogSetup.cs` | Logging: NLog configuration, log-directory selection with its fallback, and the retention sweep |
| `QueryToCsv/ConsoleMessages.cs` | Diagnostics on standard error in the `QueryToCsv: <message>` form, including the `--help` hint after a usage error |
| `QueryToCsv/ApplicationVersion.cs` | The application name and the version display text |
| `QueryToCsv/ConsoleUi.cs` | CSV encoding-name resolution shared by the one-liner and interactive modes, and the interactive prompts |
| `QueryToCsv/QueryExecutor.cs` | Database access and CSV output: the SELECT-only check on the T-SQL parse tree, connection and query execution, and CSV writing |
| `QueryToCsv.Tests/TempDirectory.cs` | Test scratch directories: one throwaway directory under the temp folder per test |
| `QueryToCsv.Tests/ConsoleStreamsDefinition.cs` | The test collection for tests that replace the process-wide standard input and error streams, run one at a time |
| `QueryToCsv.Tests/TestSetup.cs` | Test-assembly initialization: registering the code-page encodings the application registers at startup |
