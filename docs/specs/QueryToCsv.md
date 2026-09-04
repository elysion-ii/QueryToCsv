---
status: active
created: 2026-07-27
---

# QueryToCsv Specification

What QueryToCsv does. Behavior only — class structures, libraries, and internal
algorithms do not belong here. Spec-first: when a change requires behavior not written
here, update this document **before** implementing (see the Specifications section of
`docs/rules/standard.md`).

Source of truth precedence: table-driven tests > this document > current code.
Normative input → output details migrate into tests over time; when tests and this
document disagree, the tests win — fix this document.

## Purpose

Exporting a SQL Server result set to CSV normally means opening a management client,
running the query by hand, and using its export dialog — which produces inconsistent
delimiters, encodings, and NULL handling, and cannot be scripted. QueryToCsv turns that
into a repeatable operation: a folder of reviewed `.sql` files, one command, and a CSV
whose format is fixed by configuration.

## Scope

The behavior of the `QueryToCsv` executable: its two run modes (interactive and
one-liner), its auxiliary modes (help, version, open), configuration, CSV output,
logging, and error reporting.

Projects: `QueryToCsv` (the application), `QueryToCsv.Tests` (its tests).

## Users and External Systems

- **Users**: operators and developers who need result sets as files — ad hoc extracts,
  scheduled tasks, and scripts.
- **Microsoft SQL Server**: the only external system. Reached with a connection string
  from configuration, using SQL Server authentication or Windows authentication.
- **The local file system**: `.sql` inputs, CSV outputs, log files, and the
  configuration file, all next to the executable by default.

## Required Behavior

### QueryToCsv-001: Mode selection

The first argument decides the mode:

| Arguments | Mode |
|---|---|
| none | Interactive |
| `-h` or `--help` first | Help |
| `-V` or `--version` first | Version |
| `--open <target>` | Open |
| anything else | One-liner |

Help prints usage and exits 0 without loading configuration. Version prints
`QueryToCsv X.Y.Z`, with the version inherited from `Directory.Build.props` and no
build metadata, then exits 0 without loading configuration. Open acts on the target and
exits. Both other modes load and validate configuration first.

Help contains a one-line summary, usage forms, every option, the open targets, and
representative examples.

### QueryToCsv-002: Configuration loading

Configuration is read from `appsettings.json` in the executable's directory. Relative
`QueryFolder` and `OutputFolder` values resolve against that same directory. A missing
file, unparseable JSON, or a value that fails validation (QueryToCsv-003) ends the run
with exit code 1.

### QueryToCsv-003: Configuration validation

| Key | Requirement |
|---|---|
| `Connections` | At least one entry; every entry has a non-blank `Name` and `ConnectionString` |
| `QueryFolder` | Non-blank, and the folder exists |
| `OutputFolder` | Non-blank; the folder is created on demand and need not exist yet |
| `QueryTimeout` | Greater than 0 |
| `SqlFileEncoding` | An encoding name the runtime recognizes |
| `CsvSettings.Delimiter` | Exactly one character |
| `CsvSettings.NewLine` | `CRLF` or `LF`, case-sensitive |
| `CsvSettings.DateFormat` | Absent, or a usable date format string |

Each failure reports which setting is wrong and why, then exits 1.

### QueryToCsv-004: Interactive run

1. **Connection** — with one configured connection it is chosen silently; with several,
   the user picks by number from a list showing each name with its server and database.
2. **Query** — the `.sql` files in `QueryFolder` are listed in ascending file-name order
   (case-insensitive), numbered from 1, preceded by option `0` for direct entry. With no
   `.sql` files present, only option `0` is offered.
3. **Direct entry** (option `0`) — SQL is typed line by line and ended with Ctrl+Z.
   Entering nothing is an error (exit 1).
4. **Header** — the user answers `y` or `n` (case-insensitive).
5. **Encoding** — the user picks one of UTF-8, UTF-8 with BOM, UTF-16 LE, Shift-JIS.

Every prompt re-asks on invalid input. Ctrl+Z (end of input) at any prompt ends the run
with exit code 1; in direct entry it ends the SQL instead and the run continues.

### QueryToCsv-005: One-liner run

Options replace the prompts, so nothing is asked:

| Option | Long | Required | Default |
|---|---|---|---|
| `-c` | `--connection` | Only when several connections are configured | The single configured connection |
| — | `--query` | Exactly one of `--query` / `--file` | — |
| `-f` | `--file` | Exactly one of `--query` / `--file` | — |
| `-e` | `--encoding` | No | `utf-8` |
| | `--header` / `--no-header` | No | header included |

Accepted encoding names are `utf-8`, `utf-8-bom`, `utf-16`, `shift-jis` (case-insensitive,
with `utf8`, `utf8-bom`, `utf16`, `shiftjis`, `shift_jis` as accepted spellings). The
value after an option is taken verbatim, even when it looks like another option. Repeated
`--header` / `--no-header` flags: the last one wins. `-f` accepts a file name resolved in
`QueryFolder`, or an absolute path.

Long-form option values are accepted as either `--name value` or `--name=value`.
Everything after `--` is treated as an argument rather than an option. One-liner mode
accepts no positional arguments, so any argument after the separator is a usage error.
The `-q` short name is not accepted because CLI standards reserve it for quiet mode.

### QueryToCsv-006: Open mode

`--open <target>` or `--open=<target>` opens a location and exits, so the install
directory does not have to be navigated by hand.

| Target | Opens |
|---|---|
| `queries` | The configured `QueryFolder` |
| `output` | The configured `OutputFolder` |
| `config` | `appsettings.json` next to the executable |
| `log` | The `logs` folder next to the executable |
| anything else | That path, treated as a file |

If the requested file or folder does not exist, the application reports the error and
exits 1. Omitting the value after `--open` is a usage error and exits 2.

### QueryToCsv-007: SELECT-only enforcement

Before anything reaches the server, comments and string literals are removed from the
statement text and the remainder is scanned for data-modifying and out-of-scope keywords
(`INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `CREATE`, `TRUNCATE`, `EXEC`, `EXECUTE`,
`MERGE`, `GRANT`, `REVOKE`, `DENY`, `BULK`, `INTO`, `OPENROWSET`, `OPENDATASOURCE`,
`OPENQUERY`). A match rejects the statement with exit code 1 and nothing is sent.

Matching is on whole words, so identifiers that merely contain a keyword (`CreateDate`,
`UpdateLog`) remain usable.

### QueryToCsv-008: Query execution

The statement runs with `QueryTimeout` as its command timeout. Rows stream to the output
file as they arrive, so memory use does not grow with the result size. Only the first
result set is exported.

### QueryToCsv-009: Output file naming

Files are written to `OutputFolder`, named `{query}_{yyyyMMdd_HHmmss}.csv` — for direct
or inline SQL, which has no query name, `{yyyyMMdd_HHmmss}.csv`. When that name is
taken, `_2` is appended, then `_3`, and so on until a free name is found. An existing
file is never overwritten.

### QueryToCsv-010: CSV content

- The header row carries the result set's column names, and is written only when headers
  are requested.
- RFC 4180 quoting: a field containing the delimiter, a line break, or a double quote is
  enclosed in double quotes, and inner double quotes are doubled.
- `NULL` is written as `CsvSettings.NullValue`.
- Dates use `CsvSettings.DateFormat` when set, otherwise the invariant-culture default.
- Numbers use invariant culture: `.` as the decimal point, no thousands separator.
- The delimiter and line terminator come from `CsvSettings`; the file is written in the
  selected encoding, with a BOM only for UTF-8 with BOM and UTF-16 LE.
- A result set with no rows still produces a file: header-only when headers are
  requested, empty otherwise.

### QueryToCsv-011: Logging

Each run appends to a log file in `logs/` next to the executable, rotated daily and kept
for `LogRetentionDays` days. Log entries are in English and record the run's start and
end with its exit code, the selected connection, query, header choice and encoding, the
written file and its row count, and any failure. Log entries never contain the
connection string.

## Inputs and Outputs

| Direction | Item |
|---|---|
| Input | `appsettings.json` next to the executable |
| Input | `.sql` files in `QueryFolder`, read with `SqlFileEncoding` |
| Input | Command-line options (QueryToCsv-005) and interactive answers (QueryToCsv-004) |
| Input | The result set returned by SQL Server |
| Output | One CSV file in `OutputFolder` per successful run |
| Output | Progress and result messages on standard output, errors on standard error |
| Output | Log entries under `logs/` |
| Output | Exit code 0 (success), 1 (runtime error), or 2 (usage error) |

## Error Behavior

Every error prints a message beginning with `QueryToCsv: ` to standard error. Runtime
errors exit 1. Usage errors exit 2 and print
`Try 'QueryToCsv --help' for more information.` on the following line. No partial CSV
is left behind.

| Scenario | Message |
|---|---|
| `appsettings.json` missing | `QueryToCsv: appsettings.json not found.` |
| Configuration unparseable | `QueryToCsv: failed to load appsettings.json.` |
| Invalid configuration value | `QueryToCsv: ` plus the setting and the reason |
| `QueryFolder` does not exist | `QueryToCsv: QueryFolder not found: <path>` |
| Non-SELECT statement | `QueryToCsv: only SELECT statements are allowed.` |
| Connection or execution failure | `QueryToCsv: query execution failed.` |
| Query timeout | `QueryToCsv: query timed out.` |
| Direct entry left empty | `QueryToCsv: no query entered.` |
| `--query` and `--file` together | `QueryToCsv: options '--query' and '--file' cannot be used together.` |
| Neither `--query` nor `--file` with other options | `QueryToCsv: either '--query' or '--file' is required when using one-liner options.` |
| Option given without its value | `QueryToCsv: option '<option>' requires a value.` |
| Value supplied to a flag | `QueryToCsv: option '<option>' does not accept a value.` |
| Unknown option | `QueryToCsv: unknown option '<option>'` |
| Positional argument | `QueryToCsv: unexpected argument '<argument>'` |
| `-c` naming an unconfigured connection | `QueryToCsv: connection "<name>" not found.` |
| `-c` omitted with several connections | `QueryToCsv: option '--connection' is required when multiple connections are configured.` |
| `-f` file not found | `QueryToCsv: SQL file not found: <path>` |
| Unknown `-e` value | `QueryToCsv: unknown encoding "<name>". Use: utf-8, utf-8-bom, utf-16, shift-jis` |
| Requested `--open` target does not exist | `QueryToCsv: file not found: <path>` / `QueryToCsv: folder not found: <path>`; for `output`, a note that running a query creates it |

A query returning zero rows is not an error: the file is written and the run exits 0.

## Invariants

- No statement that fails the SELECT-only check is sent to the server.
- The CSV appears at its final path complete or not at all — a run interrupted mid-write
  leaves no file at that path.
- An existing output file is never overwritten or appended to.
- Every path the application resolves is relative to the executable's directory, never
  to the current working directory, so behavior does not depend on how it is launched.
- `appsettings.json` is only ever read, never written.

## Non-Functional Requirements

- Memory use is independent of result-set size: rows are streamed, not buffered.
- Ships as a self-contained single-file x64 Windows executable; no runtime installation
  is required on the target machine.
- Running the executable leaves nothing behind outside the application's own folders: it
  unpacks no content into the temp folder, so repeated releases do not accumulate
  undeleted directories there.
- The installer places the application per-user and needs no administrator rights; an
  upgrade replaces the executable and preserves the existing `appsettings.json`.
- An upgrade performed while QueryToCsv is running reports which processes hold the
  files it replaces, closes them once the user agrees, and starts them again when the
  upgrade is finished.
- An uninstall performed while QueryToCsv is running offers three answers: close it and
  continue, re-check after the user has closed it by hand, or cancel and leave the
  application installed. Nothing is closed forcibly. An unattended uninstall closes the
  application and continues, and an uninstall whose detection cannot run proceeds
  regardless.

## Out of Scope

- Non-SELECT statements, and any form of write access to the database.
- Databases other than Microsoft SQL Server.
- Result sets beyond the first one returned by a statement.
- Parameterized queries, running several queries in one invocation, and scheduling.
- Output formats other than CSV.

## Requirements-to-Tests Mapping

| Requirement | Tests |
|---|---|
| QueryToCsv-001 (mode selection and version text) | `CliInvocationTests`, `ApplicationVersionTests` |
| QueryToCsv-003 | `AppSettingsTests` |
| QueryToCsv-005 | `CliRunArgsTests` |
| QueryToCsv-007 | `QueryExecutorTests.IsSelectOnly_Statement_MatchesExpectation` |
| QueryToCsv-009 | `QueryExecutorTests.BuildOutputPath_*` |
| QueryToCsv-010 (encoding selection) | `ConsoleUiTests` |

QueryToCsv-001 behavior after mode selection, -002, -004, -006, -008, -011 and the CSV
writing side of -010 are not yet covered by tests; this document is their source of
truth until they are.
