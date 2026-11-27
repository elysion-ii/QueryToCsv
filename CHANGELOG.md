# Changelog

All notable changes to QueryToCsv are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

The version itself is defined in `Directory.Build.props`; `build/Installer.ps1` refuses
to build an installer for a version that has no heading here.

## [Unreleased]

### Changed

- **Breaking**: SQL Server authentication takes its user name and password from Windows
  Credential Manager, named by the new `Connections[].Credential`. A connection string
  that contains `Password`/`PWD` or `User ID`/`UID` is rejected at startup; store the
  credential with `cmdkey /generic:<name> /user:<user> /pass` and name it instead.
- **Breaking**: a connection string that sets `TrustServerCertificate=True` or turns
  encryption off is rejected at startup. Give the server a trusted certificate, or pin it
  with `Server Certificate=<path>`.
- **Breaking**: connection names must be unique (ignoring case), and a connection string
  that cannot be parsed is reported at startup instead of when it is used.
- **Breaking**: CSV values use ISO 8601 dates (`yyyy-MM-dd`, `yyyy-MM-ddTHH:mm:ss.fffffff`,
  and the offset for `datetimeoffset`) instead of the invariant culture's `MM/dd/yyyy`
  form, which also dropped fractional seconds. `bit` is written as `1`/`0`, binary values
  as `0x` followed by hexadecimal digits instead of `System.Byte[]`, and `decimal(38)`
  values beyond .NET's range exactly instead of failing.
- **Breaking**: text beginning with `=`, `+`, `-`, `@`, a tab, or a carriage return is
  written with a leading `'`, so spreadsheets do not evaluate it as a formula.
- **Breaking**: a character the chosen CSV encoding cannot represent, and an invalid byte
  in a `.sql` file, end the run naming the row and column or the byte offset; nothing is
  replaced with `?` or U+FFFD any more.
- **Breaking**: `CsvSettings.Delimiter` rejects `"`, a line break, and a space;
  `LogRetentionDays` accepts 1 to 3650. An empty option value (`--query ""`, `-f ""`,
  `--open ""`) is a usage error, and a relative `-f` path that leaves `QueryFolder` is
  rejected.
- **Breaking**: the SELECT-only check also rejects `NEXT VALUE FOR` and linked-server
  names (`server.database.schema.object`).
- Log entries carry ISO 8601 times with the UTC offset; `--open log` opens the folder
  logs are actually written to.
- The CSV is assembled under `%TEMP%\QueryToCsv` and placed in `OutputFolder` only when
  complete; temporary files left by a killed run are removed after 24 hours. Two runs that
  finish in the same second both succeed with different names.
- Ctrl+C cancels the run cleanly: no CSV, `QueryToCsv: cancelled.`, exit code 1.
- Uninstalling keeps `appsettings.json`, `queries`, `output`, and `logs`, and rewrites
  `PATH` only to remove QueryToCsv's own entry. An unattended upgrade fails without
  changing anything when a running QueryToCsv does not close.

- **Breaking**: the SELECT-only check parses the query as T-SQL and accepts only a single
  `SELECT` statement, optionally preceded by `WITH` common table expressions. The keyword
  scan it replaces let other statements through when they followed a `SELECT` without a
  semicolon (`SELECT 1 SHUTDOWN`) or used a word not on its list (`DBCC`, `KILL`,
  `BACKUP`, a bare procedure name). Now rejected as well: several statements in one query
  (`SELECT 1; SELECT 2`), a `GO`-separated second batch, `OPENROWSET(BULK ...)`, empty
  text, and text that is not valid T-SQL. Bracketed identifiers that are keywords, such as
  `[Update]`, are now accepted.

### Fixed

- Ending input with Ctrl+Z at a prompt now records the end of the run and its exit code
  in the log, which the run previously skipped.
- Failures that end a run — an unknown `-e` encoding, a `-c` connection that is not
  configured, a missing `--connection` with several connections, a missing `-f` file, and
  an empty direct entry — are now written to the log as well as to standard error.
- A value holding a line feed (with `CRLF` line endings) or a carriage return is now
  quoted; it was written unquoted and split the record for every CSV reader.
- A very large `LogRetentionDays` no longer ends every run with
  "unexpected application failure.", and a log folder that cannot be listed no longer
  ends the run.
- An output folder that cannot be written, a full disk, an unreadable `.sql` file or
  `QueryFolder`, and a CLR column type such as `hierarchyid` are reported with what
  failed instead of "unexpected application failure.".
- `.sql` files and SQL text have a size limit (4 MiB and 1,048,576 characters), so memory
  use stays bounded.

## [3.0.0] - 2026-09-07

### Changed

- **Breaking**: standard output now carries only what a run produces — the help text, the
  version line, and the completion report naming the written file and its row count. The
  banner, prompts, progress, hints, warnings, and errors moved to standard error, so
  redirecting standard output yields the result alone. A script that captured progress
  lines from standard output no longer receives them.
- **Breaking**: log files are named `QueryToCsv_yyyyMMdd.log`, one per day, replacing
  `QueryToCsv.log` and its dated archives. Files older than `LogRetentionDays` are now
  actually deleted on each run — the previous setting had no effect. When `logs/` next to
  the executable cannot be written to, `QueryToCsv\logs` under the user's local
  application data takes its place.
- **Breaking**: configuration no longer stops the run. A missing or unparseable
  `appsettings.json`, and any value that is blank, unusable, or out of range, falls back
  to a built-in default; each rejected value is reported once on standard error and
  written to the log, naming the setting, the value that was rejected, and the default
  applied. A run that previously ended with exit code 1 for such a value now completes.
  `Connections` is the one exception — without a usable connection there is no server to
  query, so that still ends the run with exit code 1. `QueryFolder` and `OutputFolder`
  now default to `queries` and `output` next to the executable, so neither has to be
  configured.

## [2.1.0] - 2026-09-04

### Added

- The installer handles QueryToCsv being open. An upgrade closes the running application
  and starts it again once the files are replaced; an uninstall offers to close it, to
  re-check after you have closed it yourself, or to cancel and leave QueryToCsv
  installed. Nothing is closed forcibly, and an unattended uninstall closes the
  application and continues.

### Changed

- Development rules adopt dev-standards v4.16.0: `docs/rules/standard.md` and
  `docs/rules/dotnet.md` gain the declarative-form and deferred-execution rules, and
  `.editorconfig` turns a second enumeration of an `IEnumerable` (CA1851) into a build
  error.

## [2.0.0] - 2026-07-30

### Added

- `-V` and `--version` print `QueryToCsv X.Y.Z` without loading configuration.
- Long options accept both `--name value` and `--name=value`, and `--` is recognized as
  the option separator.

### Changed

- **Breaking CLI change**: the reserved `-q` short option is no longer accepted. Use
  `--query` for an inline query.
- Help output now follows the shared CLI structure and documents every supported mode
  and option.
- Usage errors now use exit code 2, include a `--help` hint, and are distinguished from
  runtime errors, which continue to use exit code 1.
- Error messages now use the `QueryToCsv:` prefix and avoid exposing database or
  internal exception details.

### Fixed

- Relative paths passed to `--open` are now resolved from the executable directory.

## [1.5.0] - 2026-07-27

Maintenance release. The application behaves exactly as 1.4.1 did; everything here is
build, test, and release machinery. Users upgrading from 1.4.1 gain no new features and
need no configuration change.

### Added

- Test suite (`QueryToCsv.Tests`): 115 tests over command-line option parsing,
  configuration validation, SELECT-only enforcement, output file naming, and encoding
  resolution.
- This changelog. `build/Installer.ps1` refuses to build an installer for a version that
  has no heading here.
- A written specification (`docs/specs/QueryToCsv.md`) describing current behavior, and
  the shared development rules under `docs/rules/`.

### Changed

- Build scripts live in `build/` (previously `Build/`), and the installer script is
  `build/Setup_QueryToCsv.iss`, built through `build/Installer.ps1` rather than by
  invoking ISCC directly.
- The application version is defined only in `Directory.Build.props`. The installer
  script has no version of its own and fails to compile unless the build injects one.
- `build/Build.ps1` verifies code formatting and runs the test suite before publishing;
  either failure aborts the build.
- Compiler and analyzer warnings are build errors.

## [1.4.1] - 2026-05-08

### Fixed

- The installer no longer duplicates PATH entries on reinstall. The dedup check compared
  a literal `{app}` placeholder against the expanded PATH, so every reinstall appended
  another copy of the install directory. The installer now expands the target path,
  removes existing duplicates from the user PATH, and appends a single fresh entry;
  uninstall strips every duplicate.

## [1.4.0] - 2026-03-03

### Added

- One-liner execution mode. `-c`/`--connection`, `-q`/`--query`, `-f`/`--file`,
  `-e`/`--encoding`, `--header`, and `--no-header` run a query without any prompt, for
  scripts, scheduled tasks, and one-off commands. Passing `-q` or `-f` selects this mode;
  omitting both keeps the interactive flow.
- MIT license.

## [1.3.1] - 2026-03-03

### Added

- Multi-connection support. `appsettings.json` defines named connections and the tool
  asks which one to use, skipping the prompt when only one is configured.
- `-h` / `--help` prints usage and exits.

### Changed

- **Breaking configuration change**: the single `ConnectionString` key is replaced by a
  `Connections` array of `{ Name, ConnectionString }` entries. Existing configuration
  files must be updated by hand; the installer preserves `appsettings.json` on upgrade.
- The prohibited keyword list also rejects `SELECT INTO`, `OPENROWSET`,
  `OPENDATASOURCE`, and `OPENQUERY`.

### Fixed

- Parse failures surface instead of being swallowed: bare `catch` blocks were replaced
  with typed exception handling.
- An empty `QueryFolder` or `OutputFolder` reports a validation error instead of
  crashing in path resolution.
- Invalid answers at the header (`y`/`n`) and encoding (`1`–`4`) prompts report the
  error and re-prompt instead of silently looping.
- `--open output` before the first query explains that running a query creates the
  folder, instead of reporting a generic missing folder.
- `--open queries` and `--open output` with an unconfigured folder name the
  configuration key at fault instead of opening a wrong path.
- Exceptions are logged once; the previous `logger.Error(ex, ex.Message)` call recorded
  the message twice.
- A failed or timed-out query no longer leaves a partial `.csv` behind. Rows are written
  to a `.tmp` file that is renamed only on success.

## [1.2.0] - 2026-03-02

### Added

- Direct SQL input from the console. Option `0` in the query menu accepts a query typed
  in line by line, ended with Ctrl+Z; empty input is rejected.
- `BULK` is added to the prohibited keyword list.

## [1.1.0] - 2026-03-02

### Added

- `--open <target>` opens the `queries` folder, the `output` folder, `appsettings.json`,
  the `log` folder, or an arbitrary path, then exits — useful when the per-user install
  directory is awkward to reach.

## [1.0.0] - 2026-03-02

### Added

- Interactive selection of a `.sql` file from a configured folder, followed by execution
  against Microsoft SQL Server and CSV output.
- Streaming execution: memory use does not grow with the result-set size.
- RFC 4180 compliant CSV, with configurable delimiter, NULL representation, line
  terminator, and date format.
- Choice of output encoding: UTF-8, UTF-8 with BOM, UTF-16 LE, Shift-JIS.
- Optional header row.
- SELECT-only enforcement: statements containing data-modifying keywords are rejected
  before anything reaches the server.
- File logging with daily rotation and configurable retention (`LogRetentionDays`).
- Distribution as a self-contained single-file executable and a per-user installer that
  needs no administrator rights, creates the `queries/` and `output/` folders, and
  optionally registers the install directory in the user `PATH`.
