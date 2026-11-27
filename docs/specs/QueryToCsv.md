---
status: active
created: 2026-07-27
---

# QueryToCsv Specification

What QueryToCsv does. Behavior only — class structures, libraries, and internal
algorithms do not belong here. Spec-first: when a change requires behavior not written
here, update this document **before** implementing (see the Specifications section of
`docs/rules/standard.md`).

Every requirement below carries an ID of the form `QTC-<number>` and is verified by at
least one test that names that ID. Source of truth precedence: table-driven tests > this
document > current code.

## Purpose

Exporting a SQL Server result set to CSV normally means opening a management client,
running the query by hand, and using its export dialog — which produces inconsistent
delimiters, encodings, and NULL handling, and cannot be scripted. QueryToCsv turns that
into a repeatable operation: a folder of reviewed `.sql` files, one command, and a CSV
whose format is fixed by this document and the configuration.

## Scope

The behavior of the `QueryToCsv` executable — its run modes (interactive and one-liner),
its auxiliary modes (help, version, open), configuration, CSV output, logging, and error
reporting — and of its installer.

Projects: `QueryToCsv` (the application), `QueryToCsv.Tests` (unit and boundary tests),
`QueryToCsv.EndToEndTests` (end-to-end tests of the built executable), `QueryToCsv.TestSupport`
(test infrastructure shared by both test projects).

## Users and External Systems

- **Operators**: the people who install QueryToCsv, edit `appsettings.json`, place `.sql`
  files, and run it — by hand, from scripts, or from scheduled tasks. An operator can
  change every input QueryToCsv reads: the command line, standard input, the
  configuration file, the `.sql` files, and the credentials stored for the operator's
  Windows account.
- **Microsoft SQL Server**: the only external system. Reached over an encrypted connection
  whose server certificate is validated unless the operator sets
  `TrustServerCertificate` (QTC-7), using Windows authentication or SQL Server
  authentication with credentials held in Windows Credential Manager (QTC-8).
- **The local file system**: `.sql` inputs, CSV outputs, log files, the configuration
  file, and the temporary folder.

## Modes

### QTC-1: The first argument selects the mode

| Arguments | Mode |
|---|---|
| none | Interactive (QTC-12) |
| first argument `-h` or `--help` | Help (QTC-2) |
| first argument `-V` or `--version` | Version (QTC-3) |
| first argument `--open` or `--open=<target>` | Open (QTC-4) |
| anything else | One-liner (QTC-9) |

Only the first argument is examined for help, version, and open; arguments after `-h`,
`--help`, `-V`, or `--version` are ignored. `--open` in any other position is an unknown
one-liner option (QTC-9). Help, version, and open neither validate the connections nor
write log entries, because they do no export.

### QTC-2: Help

Help prints to standard output, in this order: the version line, a one-line summary, the
usage forms, every option with its short and long form, the open targets, representative
examples, and how to cancel (QTC-25). It exits 0 without reading configuration.

### QTC-3: Version

Version prints `QueryToCsv X.Y.Z` to standard output — the version the build defines,
with no build metadata — and exits 0 without reading configuration.

### QTC-4: Open mode

`--open <target>` or `--open=<target>` opens a location so the installation directory
does not have to be navigated by hand, then exits.

| Target (case-insensitive) | Opens |
|---|---|
| `queries` | `QueryFolder` (QTC-6), as a folder |
| `output` | `OutputFolder` (QTC-6), as a folder |
| `config` | `appsettings.json` next to the executable, with its associated application |
| `log` | The folder log files are written to (QTC-24), as a folder |
| anything else | That path as a file, with its associated application; a relative path resolves against the executable's directory |

Target names are compared without regard to case because Windows file names are
case-insensitive: distinguishing `QUERIES` from `queries` would only turn the keyword into
a path naming the same folder.

- `queries` and `output` read configuration and report rejected values (QTC-6) on
  standard error; the other targets do not read it.
- An omitted or empty target (`--open`, `--open=`, `--open ""`) is a usage error
  (QTC-13), as is any argument after the target.
- A target that does not exist exits 1 with `QueryToCsv: file not found: <path>` or
  `QueryToCsv: folder not found: <path>`; for `output` the message is
  `QueryToCsv: output folder does not exist yet. Run a query first to create it.`; for
  `log` when no log folder is in use (QTC-24) it is
  `QueryToCsv: logging is unavailable on this machine.`
- When the target exists but the shell cannot open it, the run exits 1 with
  `QueryToCsv: failed to open target: <path>`.
- Otherwise the run exits 0 once the shell has been asked to open the target.

## Configuration

### QTC-5: Reading the configuration file

Configuration is read once per run, at its start, from `appsettings.json` in the
executable's directory; a change to the file takes effect on the next run.

| File state | Outcome |
|---|---|
| Missing | `QueryToCsv: appsettings.json not found; continuing with built-in defaults.` on standard error and in the log; every setting takes its default |
| Present but unreadable, or not valid JSON | `QueryToCsv: failed to load appsettings.json; continuing with built-in defaults.`; every setting takes its default |
| Readable JSON | Each setting is resolved by QTC-6 and QTC-7 |

A key the application does not read (for example a misspelled one) is ignored, and the
setting it was meant to be takes its default without a message: an absent setting
rejects nothing (`docs/rules/QueryToCsv.md`, Required Input). Because `Connections` has no
default, a missing or unreadable file always ends a run mode with the QTC-7 error.

### QTC-6: Preference settings and their defaults

Every setting except `Connections` has a built-in default. A value that is present but
unusable is reported once — on standard error as `QueryToCsv: <message>` and in the log —
naming the setting, the rejected value, and the default applied; the run continues on the
default. The replacement happens while the configuration is read, so no later step sees a
rejected value.

| Key | Default | Accepted values | Message when rejected |
|---|---|---|---|
| `QueryFolder` | `queries` next to the executable | A folder path naming an existing folder; a relative path resolves against the executable's directory | `QueryFolder is blank; using "<default>".` / `QueryFolder "<value>" is not a usable folder path; using "<default>".` / `QueryFolder "<path>" does not name an existing folder; using "<default>".` |
| `OutputFolder` | `output` next to the executable | A folder path; relative resolves against the executable's directory; created when the first CSV is written | `OutputFolder is blank; using "<default>".` / `OutputFolder "<value>" is not a usable folder path; using "<default>".` |
| `QueryTimeout` | `30` | A whole number of seconds from 1 to 2147483647 | `QueryTimeout "<value>" is not a whole number from 1 to 2147483647; using 30.` |
| `SqlFileEncoding` | `UTF-8` | An encoding name the runtime recognizes, including `Shift-JIS` | `SqlFileEncoding "<value>" is not an encoding the runtime recognizes; using "UTF-8".` |
| `LogRetentionDays` | `30` | A whole number of days from 1 to 3650 | `LogRetentionDays "<value>" is not a whole number from 1 to 3650; using 30.` |
| `CsvSettings.Delimiter` | `,` | Exactly one character other than `"`, carriage return, line feed, and space | `CsvSettings.Delimiter "<value>" is not one character other than a double quote, a line break, or a space; using ",".` |
| `CsvSettings.NullValue` | An empty string | Any string | Never rejected |
| `CsvSettings.NewLine` | `CRLF` | `CRLF` or `LF`, case-sensitive | `CsvSettings.NewLine "<value>" is not "CRLF" or "LF"; using "CRLF".` |
| `CsvSettings.DateFormat` | Absent | A .NET custom or standard date and time format string that formats a date without error | `CsvSettings.DateFormat "<value>" is not a usable date format; dates use the ISO 8601 default.` |

- Whole numbers accept surrounding whitespace and a leading `+`; anything else that is
  not a base-10 integer in range is rejected.
- The delimiter cannot be a double quote, a line break, or a space because each of those
  already has a meaning in CSV field quoting or would be trimmed by readers.
- The upper bound of `LogRetentionDays` keeps retention a bounded window (ten years);
  `QueryTimeout` has no bound below the largest value the server accepts.

### QTC-7: Connections

`Connections` is an array of entries with `Name`, `ConnectionString`, and an optional
`Credential`. It is an installation setting with no default: no built-in value can name
the server an operator means to query. When it is unusable, a run mode reports the first
problem found — entries in array order, and within an entry the checks in the order
below — on standard error and in the log, and exits 1 before connecting.

| Problem | Message |
|---|---|
| No entry | `Connections must contain at least one entry.` |
| `Name` missing or blank | `Connections[<index>].Name is required.` |
| `Name` equal to an earlier entry's, ignoring case | `Connections[<index>].Name "<name>" is already used by Connections[<earlier index>].` |
| `ConnectionString` missing or blank | `Connections[<index>].ConnectionString is required.` |
| `ConnectionString` not parseable as a SQL Server connection string | `Connections[<index>].ConnectionString is not a valid connection string.` |
| `ConnectionString` sets `Encrypt` to `False`, `No`, or `Optional` | `Connections[<index>].ConnectionString must not turn encryption off.` |
| `ConnectionString` contains `Password` or `PWD` | `Connections[<index>].ConnectionString must not contain a password; store it in Windows Credential Manager and name it in Connections[<index>].Credential.` |
| `ConnectionString` contains `User ID` or `UID` | `Connections[<index>].ConnectionString must not contain a user name; it comes from Connections[<index>].Credential.` |
| `Credential` present but blank | `Connections[<index>].Credential is blank.` |

The encryption and password checks exist because an unencrypted connection can be read
on the way, and a password in a plain-text file can be read by anyone who can read the
installation folder (`docs/rules/db.md`, `docs/rules/security.md`).

**Insecure certificate setting.** An entry whose `ConnectionString` sets
`TrustServerCertificate` to true is accepted and connects as configured, even though the
setting lets the connection accept a server certificate nobody validated, which allows it
to be intercepted. When the connections are usable, a run mode reports one warning per
such entry, in array order, on standard error and in the log at warning level, before
choosing the connection:

`QueryToCsv: Connections[<index>] "<name>" contains an insecure setting: TrustServerCertificate=True accepts any server certificate, so the connection can be intercepted. Give the server a certificate the client trusts, or pin it with "Server Certificate".`

- The warning changes no exit code and is not an error (QTC-13).
- A run that ends on a problem in the table above reports only that problem, because it
  connects nowhere.
- The warning follows from `TrustServerCertificate` alone, whatever `Encrypt` is set to
  (`docs/rules/QueryToCsv.md`, Server Certificate Validation).
- Help, version, and open modes do not validate the connections and report nothing.
- The warning names the entry; it never contains the connection string.

The operator decides whether to accept this risk, because a server that uses its
self-generated certificate cannot be given a validated connection without reconfiguring
the server (`docs/rules/QueryToCsv.md`, Server Certificate Validation).

### QTC-8: Credentials for SQL Server authentication

A connection entry with `Credential` uses SQL Server authentication: the user name and
password are read, when that connection is used, from the Windows Credential Manager
generic credential whose target name is the `Credential` value, stored for the Windows
account running QueryToCsv (for example with
`cmdkey /generic:<name> /user:<user> /pass`). An entry without `Credential` connects with
what its connection string states, which is Windows authentication when it sets
`Integrated Security=True`.

When the named credential does not exist or cannot be read, the run exits 1 with
`QueryToCsv: credential "<name>" was not found in Windows Credential Manager.` before
connecting. The password never appears in output, in the log, or in any message.

## One-Liner Mode

### QTC-9: One-liner options

Options replace the prompts, so nothing is read from standard input:

| Short | Long | Value | Required | Default |
|---|---|---|---|---|
| `-c` | `--connection` | Connection name | Only when several connections are configured | The single configured connection |
| — | `--query` | SQL text | Exactly one of `--query` / `--file` | — |
| `-f` | `--file` | File name or path | Exactly one of `--query` / `--file` | — |
| `-e` | `--encoding` | Encoding name (QTC-10) | No | `utf-8` |
| — | `--header` / `--no-header` | none | No | header included |

- Long options accept `--name value` and `--name=value`; short options accept `-x value`.
  The value after an option is taken verbatim, even when it looks like an option.
- A value option given without a value, or with an empty value, is a usage error
  (`option '<option>' requires a value.`). A value given to `--header` or `--no-header`
  is a usage error (`option '<option>' does not accept a value.`).
- When an option is repeated, its last occurrence wins.
- `--query` together with `--file` is a usage error
  (`options '--query' and '--file' cannot be used together.`); neither of them is a
  usage error (`either '--query' or '--file' is required when using one-liner options.`).
- Everything after `--` is a positional argument; one-liner mode accepts none, so any is
  a usage error (`unexpected argument '<argument>'`). An unrecognized option is a usage
  error (`unknown option '<option>'`). `-q` is not accepted, because the CLI standard
  reserves it for quiet mode.
- `-f` with an absolute path reads that file. `-f` with a relative path resolves inside
  `QueryFolder`; a relative path that resolves outside `QueryFolder` (for example through
  `..`) is a usage error (`SQL file "<value>" is outside QueryFolder; give an absolute path instead.`),
  because a relative name is a name within that folder.
- A `-f` file that does not exist exits 1 with `QueryToCsv: SQL file not found: <path>`.

### QTC-10: CSV encoding names

| Name (case-insensitive) | Accepted spellings | Encoding written |
|---|---|---|
| `utf-8` | `utf8` | UTF-8 without BOM |
| `utf-8-bom` | `utf8-bom` | UTF-8 with BOM |
| `utf-16` | `utf16` | UTF-16 little-endian with BOM |
| `shift-jis` | `shiftjis`, `shift_jis` | Shift-JIS as Windows code page 932, without BOM |

Any other value of `-e` is a usage error
(`unknown encoding "<name>". Use: utf-8, utf-8-bom, utf-16, shift-jis`).

### QTC-11: Choosing the connection in one-liner mode

`-c` selects the entry whose `Name` equals the value, ignoring case; names are unique
(QTC-7), so at most one matches. A value matching no entry is a usage error
(`connection "<name>" not found.`). Without `-c`, the single configured entry is used;
with several entries, omitting `-c` is a usage error
(`option '--connection' is required when multiple connections are configured.`).

## Interactive Mode

### QTC-12: Interactive run

The banner `=== QueryToCsv ===` and every prompt go to standard error.

1. **Connection** — one configured entry is chosen without asking. With several, each is
   listed as `<n>. <Name> (<server> - <database>)`, numbered from 1 in configuration
   order; a server or database the connection string does not state shows as `?`.
2. **Query** — option `0. Enter query directly`, followed by the `.sql` files directly in
   `QueryFolder` (not its subfolders), in ascending file-name order compared ordinally
   without regard to case, numbered from 1. With no `.sql` file, or no `QueryFolder`,
   only option `0` is offered. A `QueryFolder` that exists but cannot be listed ends the
   run with `QueryToCsv: QueryFolder "<path>" could not be read.`, exit 1.
3. **Direct entry** (option `0`) — SQL is typed line by line and ended with end of input
   (Ctrl+Z, Enter). Lines are joined with CRLF. Text that is empty or whitespace only ends
   the run with `QueryToCsv: no query entered.`, exit 1.
4. **Header** — `y` or `n`, case-insensitive.
5. **Encoding** — `1` UTF-8, `2` UTF-8 with BOM, `3` UTF-16 LE, `4` Shift-JIS (QTC-10).

An answer is read with surrounding whitespace removed. A number prompt accepts only ASCII
digits naming a listed number (leading zeros allowed); any other answer prints
`Please enter a number between <first> and <last>.` or `Please enter y or n.` and asks
again. Asking again repeats only on a new line of input, so it ends when a valid answer
or the end of input arrives. End of input at any prompt other than direct entry ends the
run with exit 1 and no message.

## Errors

### QTC-13: Error reporting and precedence

Every error is one line on standard error beginning with `QueryToCsv: `. A **usage error**
exits 2 and is followed by `Try 'QueryToCsv --help' for more information.`; a **runtime
error** exits 1. A run with several problems reports only the first one found, checked in
this order:

1. Command-line syntax (QTC-1, QTC-4, QTC-9) — usage error
2. The `-e` encoding name (QTC-10) — usage error, reported before configuration is read
3. Rejected preference values (QTC-6) — reported, never an error
4. Connections (QTC-7) — runtime error; then the insecure-certificate warnings (QTC-7) —
   reported, never an error
5. The `-c` connection choice (QTC-11) — usage error
6. The `-f` path outside `QueryFolder` (QTC-9) — usage error
7. In this order: the `-f` file and the SQL text (QTC-9, QTC-14), the SELECT-only check
   (QTC-15), the credential (QTC-8), the connection and query (QTC-16), and writing the
   CSV (QTC-18 to QTC-22) — runtime errors

Every error ends the run without a CSV at the output path. Every error of a run mode is
also written to the log (QTC-24).

## Query

### QTC-14: The SQL text

The SQL comes from `--query`, from direct entry, or from a `.sql` file.

- A `.sql` file is read with `SqlFileEncoding`, except that a byte order mark at its
  start (UTF-8, UTF-16 LE or BE, UTF-32 LE) selects that encoding instead and is not part
  of the text.
- A byte sequence that is invalid in the encoding used ends the run with
  `QueryToCsv: SQL file "<path>" is not valid <encoding> at byte <offset>.`, exit 1;
  nothing is sent to the server, because a replaced character changes the statement.
- SQL text is limited to 1,048,576 characters and a `.sql` file to 4,194,304 bytes;
  larger input ends the run with `QueryToCsv: SQL text exceeds 1,048,576 characters.` or
  `QueryToCsv: SQL file "<path>" exceeds 4,194,304 bytes.`, exit 1, before it is read
  whole. The limit keeps memory bounded whatever the input; a single SELECT never
  approaches it.
- A `.sql` file that exists but cannot be read ends the run with
  `QueryToCsv: SQL file "<path>" could not be read.`, exit 1.

### QTC-15: SELECT-only enforcement

Before anything reaches the server, the SQL text is parsed as T-SQL. It is accepted only
when all of the following hold:

- It parses without a syntax error.
- It holds exactly one batch, and that batch holds exactly one statement. A trailing
  semicolon is allowed.
- That statement is a `SELECT`, optionally preceded by `WITH` common table expressions.
- The `SELECT` has no `INTO` clause.
- Nothing in it reads another server or an external rowset: `OPENROWSET` (including
  `OPENROWSET(BULK ...)`), `OPENQUERY`, `OPENDATASOURCE`, or a table, view, or function
  named with a server part (`server.database.schema.object`).
- Nothing in it changes server state: no `NEXT VALUE FOR`, which advances a sequence.

Anything else ends the run with `QueryToCsv: only SELECT statements are allowed.`,
exit 1, and nothing is sent. That includes empty text, a second statement in the same
batch (with or without a separating semicolon), a second batch after `GO`, and a batch
that starts with a bare procedure name, which SQL Server would otherwise execute without
`EXEC`.

The decision is made on the parsed statement, never on the words in the text. Comments
and string literals therefore never change it, and identifiers that look like keywords
stay usable, whether they contain a keyword (`CreateDate`, `UpdateLog`) or are a
bracketed keyword (`[Update]`).

The check runs in the operator's own process and protects against a mistaken statement,
not against an operator who means to write: anyone holding the connection's credentials
can connect without QueryToCsv. Preventing writes is the job of the database account,
which must be granted read permission only (`docs/rules/db.md`, Least-privilege runtime
account).

### QTC-16: Executing the query

- The statement runs with `QueryTimeout` as its timeout, in seconds. The timeout bounds
  the cumulative time spent waiting for the server while executing and reading the
  result; time spent writing the CSV does not count. When it expires, the run ends with
  `QueryToCsv: query timed out.`, exit 1.
- Opening the connection is bounded by the connection string's `Connect Timeout`
  (15 seconds when it states none). A connection that cannot be opened, and any error the
  server returns, ends the run with `QueryToCsv: query execution failed.`, exit 1; the
  server's error is written to the log.
- Rows are written as they arrive, so memory use does not grow with the result size.
  Only the first result set is exported.
- Each invocation is its own export: running the same command again runs the query again
  and writes another file (QTC-17); nothing identifies two runs as the same request.

## Output

### QTC-17: Output file name

The CSV is named `{query}_{yyyyMMdd_HHmmss}.csv`, where `{query}` is the `.sql` file name
without its extension and the timestamp is the machine's local time at the moment the SQL
text has been accepted (QTC-15), before connecting. Inline and directly entered SQL has no query
name: `{yyyyMMdd_HHmmss}.csv`. When that name is taken, `_2` is appended, then `_3`, and
so on, and the first free name is used.

**OUTPUT-KEPT**: an existing file in `OutputFolder` is never overwritten, replaced, or
appended to. Claiming a name and placing the finished file there is one atomic step: when
two runs finish with the same name, one takes it and the other takes the next free
suffix, and both succeed.

### QTC-18: Writing the file

- `OutputFolder` is created, with any missing parent folders, when the first CSV is
  written.
- The CSV is assembled in an application-owned folder under the user's temporary folder,
  copied beside its destination under a temporary name ending in `.QueryToCsv.tmp`, and
  renamed to its final name only when complete. A reader of `OutputFolder` therefore sees
  either the whole file or no file at that name, including after a crash, and a network
  `OutputFolder` receives one copy rather than the rows as they arrive.
- Every exit path of the run deletes the temporary files it created. Each run mode, after
  reading configuration, deletes temporary files older than 24 hours that an earlier run
  left — only in the application's own temporary folder and only files in `OutputFolder`
  ending in `.QueryToCsv.tmp` — so a killed process does not leave them forever. A file
  that cannot be deleted is left for a later run and written to the log.
- When the temporary folder cannot be written, or its disk is full, the run ends with
  `QueryToCsv: cannot write the temporary file in "<folder>".`, exit 1. When
  `OutputFolder` cannot be created or written, or its disk is full, the run ends with
  `QueryToCsv: cannot write the output file in "<OutputFolder>".`, exit 1.

### QTC-19: CSV structure

- With headers requested, the first record holds the result set's column names in result
  order; a column without a name has an empty header field. Without headers there is no
  such record.
- Each row is one record, in the order the server returns rows; without `ORDER BY` in the
  query that order is the server's choice.
- Fields are separated by `CsvSettings.Delimiter`, and every record, including the last,
  ends with `CsvSettings.NewLine`.
- A field is enclosed in double quotes when it contains the delimiter, a double quote, a
  carriage return, or a line feed, or begins or ends with a space; a double quote inside
  it is doubled. Every other field is written unquoted.
- `NULL` is written as `CsvSettings.NullValue`, quoted by the same rule.
- A result set with no rows produces a file holding only the header record when headers
  are requested, and an empty file otherwise; the run exits 0.

### QTC-20: Value representation

QueryToCsv writes values as they are stored and converts no time zone; every format is
culture-invariant.

| SQL Server type | Written as |
|---|---|
| `tinyint`, `smallint`, `int`, `bigint` | Base-10 integer, `-` for negatives |
| `decimal`, `numeric`, `money`, `smallmoney` | Exact base-10 value with the column's scale, `.` as decimal point, no grouping (`12.3400` for `money`) |
| `float`, `real` | Shortest text that reads back as the same value, `.` as decimal point, exponent form `E+nn`/`E-nn` where shorter (`1E+20`) |
| `bit` | `1` or `0` |
| `date` | `yyyy-MM-dd` |
| `time` | `HH:mm:ss.fffffff` |
| `datetime`, `datetime2`, `smalldatetime` | `yyyy-MM-ddTHH:mm:ss.fffffff`, without an offset because the stored value has none |
| `datetimeoffset` | `yyyy-MM-ddTHH:mm:ss.fffffffzzz` (`+09:00`) |
| `uniqueidentifier` | 36-character lowercase hyphenated form |
| `binary`, `varbinary`, `image`, `timestamp` (`rowversion`) | `0x` followed by two uppercase hexadecimal digits per byte |
| `char`, `varchar`, `nchar`, `nvarchar`, `text`, `ntext` | The text as stored, including the padding of fixed-length types (QTC-21) |
| `xml` | The text the server returns for it, which the server normalizes (`<a/>` is returned as `<a />`) |
| `sql_variant` | By the type of the value it holds; a `date` held in `sql_variant` takes the `datetime2` form, because the provider returns both as the same type |

- `CsvSettings.DateFormat`, when set, replaces the format of `date`, `datetime`,
  `datetime2`, `smalldatetime`, and `datetimeoffset` values; `time` keeps its format.
- A column of any other type — CLR types such as `hierarchyid`, `geometry`, and
  `geography` — ends the run before the header is written with
  `QueryToCsv: column "<name>" has type <type>, which cannot be exported; convert it in the query.`,
  exit 1.

### QTC-21: Spreadsheet formula triggers

A text value (types `char`, `varchar`, `nchar`, `nvarchar`, `text`, `ntext`, and text held
by `sql_variant`) whose first character is `=`, `+`, `-`, `@`, a tab, or a carriage return
is written with a single quote `'` before it, so a spreadsheet that opens the CSV shows the
text instead of evaluating it as a formula. Values of every other type, including
negative numbers, are written unchanged.

### QTC-22: CSV encoding

The CSV is written in the encoding chosen by `-e` or the interactive prompt (QTC-10), with
a BOM only for UTF-8 with BOM and UTF-16. A character the encoding cannot represent (for
example a character outside Shift-JIS, or an unpaired surrogate) ends the run with
`QueryToCsv: the value in row <n>, column "<name>" cannot be written in <encoding>.`,
exit 1, and no CSV; rows are counted from 1, excluding the header, and a column name
that cannot be encoded reports row 0.

### QTC-23: Completion report

A successful run writes `Done: <full path of the CSV>` and `Rows: <row count>` to standard
output, then exits 0. The row count uses `,` to group thousands. Progress lines
(`Connecting...`, `Executing query...`, `Writing CSV...`) and the hints that follow the
report go to standard error.

## Logging

### QTC-24: Log files

- Each run mode appends to `QueryToCsv_yyyyMMdd.log`, one file per local calendar day,
  in `logs/` next to the executable. When that folder cannot be written to, `logs/`
  under `QueryToCsv` in the user's local application data takes its place. When neither
  can be written — including when the local application data folder does not exist — no
  log file is written. Logging never ends a run.
- At the start of each run mode, after configuration is read, this application's log
  files (`QueryToCsv_yyyyMMdd.log`) in the folder in use whose date is more than
  `LogRetentionDays` − 1 days before today are deleted; files that do not match that name
  are never touched. A file that cannot be deleted, or a folder that cannot be listed,
  leaves the run unaffected.
- Every entry begins with its time in ISO 8601 with the machine's UTC offset
  (`2026-03-02T15:30:45.123+09:00`), so entries from machines in different time zones
  compare correctly.
- Entries are in English and record: the start with the version; every rejected
  configuration value; every insecure-certificate warning (QTC-7); the selected
  connection by name; the query as the `.sql` file
  name, `[Inline]` for `--query`, or `[Direct Input]`; the header choice and encoding;
  the written file and its row count; every error of QTC-13 and its cause; and the end
  with its exit code.
- No entry contains a connection string or a password. A server error written to the log
  may name the database user it concerns (for example a failed login).

### QTC-25: Cancelling with Ctrl+C

Ctrl+C during a run mode stops the run: the query is cancelled, temporary files are
deleted (QTC-18), the run writes `QueryToCsv: cancelled.` to standard error and the end
of the run to the log, and exits 1. No CSV appears at the output path.

## Distribution

### QTC-26: Executable

QueryToCsv ships as one self-contained x64 Windows executable; no runtime installation is
required. Running it unpacks nothing into the temporary folder, so repeated releases do
not accumulate directories there.

### QTC-27: Installer

- The installer installs per user under `%LOCALAPPDATA%\Programs\QueryToCsv` and needs no
  administrator rights. It creates `queries` and `output` there.
- `appsettings.json` is written from the shipped template only when none exists, so an
  upgrade preserves the operator's configuration.
- An optional task adds the installation folder to the user `PATH`, once, at its end;
  uninstalling removes every `PATH` entry equal to that folder (ignoring case) and leaves
  every other entry, and the value's type, as they were. When no entry matches, `PATH` is
  not rewritten.
- Uninstalling removes the program files and leaves `appsettings.json`, `queries`,
  `output`, and `logs` with their contents, because they hold the operator's data.

### QTC-28: Installing and uninstalling while QueryToCsv is running

- An attended upgrade reports which processes hold the files it replaces, closes them
  once the user agrees, and starts them again when the upgrade is finished.
- An attended uninstall offers three answers: close it and continue, re-check after the
  user has closed it by hand, or cancel and leave QueryToCsv installed.
- An unattended upgrade or uninstall asks the running processes to close and proceeds
  once they have; when they do not close, it ends with a non-zero exit code and leaves the
  installed files as they were.
- Nothing is closed forcibly. An uninstall whose running-process detection cannot run
  proceeds regardless.

## Out of Scope

- Non-SELECT statements, and any form of write access to the database.
- Databases other than Microsoft SQL Server.
- Result sets beyond the first one returned by a statement.
- Parameterized queries, running several queries in one invocation, and scheduling.
- Output formats other than CSV.
