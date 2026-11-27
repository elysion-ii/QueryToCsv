---
status: active
created: 2026-11-27
---

# QueryToCsv Violating Behavior

Behavior the current implementation of QueryToCsv exhibits that breaks a rule under
`docs/rules/` or contradicts a statement the specification used to make, recorded here
instead of in `docs/specs/QueryToCsv.md` because fixing it was deferred
(`docs/rules/specs.md`, Ledgers). These entries describe what the code does, not what it
must do; the specification never states any of them as a requirement. Each entry is
resolved in the change that settles its decision, which removes the entry.

Read together with `docs/specs/QueryToCsv.md`.

## Date and time values written to the CSV

- **Decision**: how a date or time column is written when `CsvSettings.DateFormat` is not set
- **Question**: REPRESENTATION
- **Observed behavior**: `datetime`, `datetime2`, and `smalldatetime` values are written in the invariant culture's general format, `MM/dd/yyyy HH:mm:ss`, with no offset. `datetimeoffset` values are written as `MM/dd/yyyy HH:mm:ss zzz`. `date` values arrive as date-and-time and are written with a `00:00:00` time of day. The time zone the stored values are in is passed through unstated.
- **Code location**: `QueryToCsv/QueryExecutor.cs`, `FormatValueAsync`
- **Asserted by**: `README.md` (CSV Output, Format: "otherwise `InvariantCulture` default") and the warning text for an unusable `DateFormat` ("dates use the invariant-culture default"). No test covers it. Until this change, `docs/specs/QueryToCsv.md` stated it in QueryToCsv-003 and QueryToCsv-010; those statements were moved here.
- **Violated rules**: `docs/rules/values.md`, Machine-Readable Formatting (dates in an invariant standard format such as ISO 8601, never a locale's), and Time and Dates (instants carry UTC or an explicit offset; calendar dates stay distinct from instants)
- **Allowed fixes**: make ISO 8601 the default — `yyyy-MM-ddTHH:mm:ss.FFFFFFF` for date-and-time types, `yyyy-MM-dd` for `date`, and the offset included for `datetimeoffset`. This changes the default output for every consumer that parses the current format, so it is a breaking change released under a new major version. The specification also has to state what time zone a date-and-time value without an offset represents.

## Server certificate validation on the database connection

- **Decision**: whether a connection to SQL Server verifies the server's certificate
- **Question**: ORIGIN
- **Observed behavior**: the shipped configuration template, which the installer writes as the initial `appsettings.json`, and every connection-string example in the README set `TrustServerCertificate=True`. The application passes the configured connection string to SQL Server unchanged, so these connections accept any certificate.
- **Code location**: `QueryToCsv/appsettings.template.json`; `README.md` (Connection Strings, Full Example); `QueryToCsv/QueryExecutor.cs`, `ExecuteAsync`
- **Asserted by**: the template and the README examples. No test covers it.
- **Violated rules**: `docs/rules/db.md`, Access Control and Text Comparison (encrypted connections verify the server); `docs/rules/security.md`, Data Protection at Rest and in Transit
- **Allowed fixes**: drop `TrustServerCertificate=True` from the template and the README, and document how an operator gives the server a certificate the client trusts, or pins that one certificate. Optionally, also reject a configured connection string that sets `TrustServerCertificate=True` while the configuration is read, which turns the rule into a check.

## Database credentials stored in the configuration file

- **Decision**: where the credentials of a connection that uses SQL Server authentication are kept
- **Question**: ORIGIN
- **Observed behavior**: `Connections[].ConnectionString` is read from `appsettings.json` next to the executable, and with SQL Server authentication it carries the user name and password in plain text.
- **Code location**: `QueryToCsv/AppSettings.cs`, `FromConfiguration`; `QueryToCsv/appsettings.template.json`; `README.md` (Connection Strings)
- **Asserted by**: the template and the README examples. No test covers it.
- **Violated rules**: `docs/rules/security.md`, Secret Storage and Credential Management; `docs/rules/configuration.md`, which places secrets in a platform secret store rather than in configuration files
- **Allowed fixes**: keep the password in the Windows Credential Manager under a name the connection entry references; protect it with DPAPI for the current user; or support Windows authentication only. Each fix also needs a migration path for installations whose `appsettings.json` already holds passwords.

## Text the chosen encoding cannot represent

- **Decision**: what happens to a character the CSV encoding cannot represent, and to bytes in a `.sql` file that are invalid in `SqlFileEncoding`
- **Question**: REPRESENTATION (out-of-capacity handling)
- **Observed behavior**: when writing Shift-JIS, a character outside Shift-JIS is written as `?`. The UTF-8 and UTF-16 encoders replace an unpaired surrogate with U+FFFD. When a `.sql` file is read, an invalid byte sequence becomes U+FFFD, and the altered statement is executed. None of these is reported.
- **Code location**: `QueryToCsv/ConsoleUi.cs`, `ResolveEncoding` (encodings created with replacement fallback); `QueryToCsv/QueryExecutor.cs`, `WriteCsvAsync`; `QueryToCsv/QueryRun.cs`, `ReadSqlFileAsync`
- **Asserted by**: `ConsoleUiTests.ResolveEncoding_ShiftJis_RoundTripsJapaneseText` covers only text that Shift-JIS can represent. No test covers the replacement.
- **Violated rules**: `docs/rules/file-exchange.md`, Encoding and Structured Formats (reject or fail on invalid byte sequences); `docs/rules/dotnet.md`, FILEEX (encodings configured to throw); `docs/rules/standard.md`, External Input (reject, never repair)
- **Allowed fixes**: end the run with exit code 1 and no CSV, naming the row and column whose value cannot be encoded or the position of the invalid bytes in the `.sql` file; or write a marker the specification defines and report how many values were replaced.

## Spreadsheet formula triggers in exported text

- **Decision**: how a text value that begins with a spreadsheet formula trigger is written
- **Question**: REPRESENTATION
- **Observed behavior**: values beginning with `=`, `+`, `-`, `@`, a tab, or a carriage return are written unchanged, so a spreadsheet that opens the CSV evaluates them as formulas.
- **Code location**: `QueryToCsv/QueryExecutor.cs`, `WriteCsvAsync`
- **Asserted by**: nothing
- **Violated rules**: `docs/rules/file-exchange.md`, Encoding and Structured Formats (neutralize spreadsheet formula triggers according to the specification)
- **Allowed fixes**: prefix such text values with `'`, applied to text columns only so that negative numbers stay numbers; or make neutralization a setting whose default is on. Either changes the bytes that machine consumers of the CSV receive, so the specification has to state which columns it applies to.

## Where the CSV is assembled, and what an interrupted run leaves

- **Decision**: where the CSV is written while rows arrive, and how files left by an interrupted run are removed
- **Question**: PARTIAL
- **Observed behavior**: rows are written to `<output file>.tmp` inside `OutputFolder`, which is renamed to the final name on success and deleted in a `finally` block otherwise. A process that is killed mid-write leaves the `.tmp` file in `OutputFolder`, and nothing removes it later. When `OutputFolder` is a network share, the rows are written across the network as they arrive.
- **Code location**: `QueryToCsv/QueryExecutor.cs`, `ExecuteAsync` (`tempPath`)
- **Asserted by**: `CHANGELOG.md` 1.3.1 (Fixed: "Rows are written to a `.tmp` file that is renamed only on success"). The specification's invariant that the CSV appears complete or not at all still holds.
- **Violated rules**: `docs/rules/file-exchange.md`, Network-Path Staging and Temporary Files (stage locally, recover crash-orphaned temporary files); `docs/rules/client.md`, Leave No Debris on the Host Machine
- **Allowed fixes**: write under the temp folder with an application-owned name (`QueryToCsv_*`), copy the finished file next to the destination, and rename it there, so the destination still sees the whole file at once. Delete this application's leftover temporary files older than a stated age at startup. Alternatively, keep writing in `OutputFolder` and delete leftover `*.csv.tmp` files older than a stated age at startup, which leaves the network-path rule unmet.

## Unattended uninstall while QueryToCsv is running

- **Decision**: the outcome of a silent uninstall when QueryToCsv is running and does not close
- **Question**: UNAVAILABLE
- **Observed behavior**: a silent uninstall asks the running processes to close through the Restart Manager without forcing them. If any remain, the uninstall ends with a failure exit code and leaves QueryToCsv installed.
- **Code location**: `build/Setup_QueryToCsv.iss`, `InitializeUninstall`
- **Asserted by**: the comment at the silent-uninstall branch in `InitializeUninstall`
- **Violated requirement**: the specification stated, under Non-Functional Requirements, "An unattended uninstall closes the application and continues". That holds only when the application closes; the statement was moved here because the code contradicts it when the application does not close. The code's behavior is what `docs/rules/client.md` (Installation, Update, and Removal: close gracefully during unattended runs) requires.
- **Allowed fixes**: restate the requirement to match the code — an unattended uninstall closes the application gracefully, and fails with a non-zero exit code when it does not close. This is the only outcome `client.md` permits, because forcing the processes closed is prohibited.
