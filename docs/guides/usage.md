---
status: active
created: 2026-11-27
---

# QueryToCsv Usage Guide

How to configure and run QueryToCsv: the configuration file, connections, SQL files, the
run modes, the CSV it writes, its logs, and its errors. `QueryToCsv --guide` prints this
guide, so it is at hand wherever the executable is; `QueryToCsv --guide > usage.md` saves
it to a file.

## Where QueryToCsv Keeps Its Files

The installer installs QueryToCsv per user under `%LOCALAPPDATA%\Programs\QueryToCsv`
and creates there:

| Path | Holds |
|------|-------|
| `QueryToCsv.exe` | The program |
| `appsettings.json` | The configuration (created only when none exists, so upgrades keep yours) |
| `queries\` | The `.sql` files offered for selection |
| `output\` | The CSV files written |
| `logs\` | One log file per day, created on the first run |

The standalone `QueryToCsv.exe` comes without `appsettings.json`: create it next to the
executable, using the Full Example below as a starting point.

`QueryToCsv --open <target>` opens these locations without navigating to them (see Open
Folders and Files).

## Configuration

All settings are in `appsettings.json` next to the executable. It is read once, at the
start of each run; a change takes effect on the next run.

### Connections

Define one or more named connections in the `Connections` array. With one connection it
is used without asking; with several, you choose one at the start of an interactive run,
or name it with `-c` in one-liner mode. Connection names must be unique, ignoring case.

**Windows authentication** (the current Windows login):

```json
{
  "Connections": [
    { "Name": "Local", "ConnectionString": "Server=myserver;Database=mydb;Integrated Security=True;Encrypt=Mandatory;" }
  ]
}
```

**SQL Server authentication** (user name and password from Windows Credential Manager):

The connection string never holds the user name or password; a connection string that
contains `User ID`/`UID` or `Password`/`PWD` is rejected. Store them once for the Windows
account that runs QueryToCsv, then name that credential in `Credential`:

```
cmdkey /generic:"QueryToCsv/Dev Server" /user:myuser /pass
```

```json
{
  "Connections": [
    { "Name": "Dev Server", "ConnectionString": "Server=192.168.1.10;Database=SalesDB;Encrypt=Mandatory;", "Credential": "QueryToCsv/Dev Server" },
    { "Name": "Prod Server", "ConnectionString": "Server=sql-prod.local;Database=OrderDB;Encrypt=Mandatory;Server Certificate=C:\\certs\\sql-prod.cer;", "Credential": "QueryToCsv/Prod Server" }
  ]
}
```

**Encryption and the server certificate.** Every connection is encrypted: a connection
string that turns encryption off (`Encrypt=False`, `No`, or `Optional`) is rejected. The
server's certificate is validated, and a server whose certificate does not chain to a
trusted root can be pinned with `Server Certificate=<path to its .cer file>`.

A server that uses SQL Server's self-generated certificate gets a new certificate on every
service restart, so it can be neither trusted nor pinned. For such a server you can set
`TrustServerCertificate=True` and accept that the connection can be intercepted. Every run
that connects then warns about each such entry, without printing its connection string:

```
QueryToCsv: Connections[0] "Takai" contains an insecure setting: TrustServerCertificate=True accepts any server certificate, so the connection can be intercepted. Give the server a certificate the client trusts, or pin it with "Server Certificate".
```

The warning does not change the exit code. It disappears once the entry no longer sets
`TrustServerCertificate=True`: give the server a certificate the client trusts, or pin
its certificate with `Server Certificate`, then remove the setting.

> The SELECT-only check guards against mistakes, not against someone who means to write:
> give the database account read permission only.

### Settings Reference

| Key | Type | Required | Default | Description |
|-----|------|----------|---------|-------------|
| `Connections[]` | array | Yes | - | Array of named connection entries |
| `Connections[].Name` | string | Yes | - | Display name shown in the selection menu |
| `Connections[].ConnectionString` | string | Yes | - | SQL Server connection string, without user name or password, with encryption on; `TrustServerCertificate=True` is accepted with a warning |
| `Connections[].Credential` | string | No | - | Windows Credential Manager generic credential holding the SQL Server user name and password |
| `QueryFolder` | string | No | `queries` next to the executable | Folder containing `.sql` files |
| `OutputFolder` | string | No | `output` next to the executable | Folder for CSV output |
| `QueryTimeout` | int | No | `30` | Query timeout in seconds, 1 to 2147483647 |
| `SqlFileEncoding` | string | No | `"UTF-8"` | Encoding for reading `.sql` files |
| `LogRetentionDays` | int | No | `30` | Number of days to keep log files, 1 to 3650 |
| `CsvSettings.Delimiter` | string | No | `","` | Single character other than `"`, a line break, or a space. Use `"\t"` for tab |
| `CsvSettings.NullValue` | string | No | `""` | String to output for SQL NULL values |
| `CsvSettings.NewLine` | string | No | `"CRLF"` | `"CRLF"` or `"LF"` |
| `CsvSettings.DateFormat` | string | No | `null` | .NET date format string (e.g. `"yyyy-MM-dd HH:mm:ss"`) replacing the ISO 8601 default |

### Path Resolution

Relative paths in `QueryFolder` and `OutputFolder` are resolved relative to the
executable's directory, never the directory you run the command from.

### Unusable Values

`Connections` is the only required setting: without one, QueryToCsv has no server to
query and stops. Every other setting has a built-in default and falls back to it when
the file is missing, its JSON cannot be parsed, or the value is unusable. Each rejected
value is reported once on standard error and written to the log, naming the setting, the
value that was rejected, and the default applied; the run then continues on that
default. A key that is left out, or misspelled, takes its default without a message.

### Full Example

```json
{
  "Connections": [
    { "Name": "Dev Server", "ConnectionString": "Server=192.168.1.10;Database=SalesDB;Encrypt=Mandatory;", "Credential": "QueryToCsv/Dev Server" },
    { "Name": "Prod Server", "ConnectionString": "Server=sql-prod.local;Database=OrderDB;Integrated Security=True;Encrypt=Mandatory;" }
  ],
  "QueryFolder": "./queries",
  "OutputFolder": "./output",
  "QueryTimeout": 60,
  "SqlFileEncoding": "UTF-8",
  "LogRetentionDays": 30,
  "CsvSettings": {
    "Delimiter": "\t",
    "NullValue": "NULL",
    "NewLine": "CRLF",
    "DateFormat": "yyyy-MM-dd HH:mm:ss"
  }
}
```

## SQL Files

Place `.sql` files in the `queries` folder (or whichever folder `QueryFolder` points to).
Each file holds exactly one SELECT statement, optionally preceded by `WITH` common table
expressions. Anything else is rejected before it reaches the server: a data-modifying
statement, `SELECT ... INTO`, a second statement or a `GO`-separated batch, a read through
`OPENROWSET`, `OPENQUERY`, `OPENDATASOURCE`, or a linked server
(`server.database.schema.table`), or `NEXT VALUE FOR`.

A `.sql` file is read with `SqlFileEncoding`, unless it starts with a byte order mark,
which selects its encoding instead. A file is limited to 4 MiB and its text to 1,048,576
characters; a byte that is invalid in the encoding is reported with its offset instead of
being replaced.

## Running QueryToCsv

### Interactive Mode

Run QueryToCsv without arguments and answer the prompts: the connection (when several
are configured), the query, whether to include a header row, and the CSV encoding.

```
=== QueryToCsv ===

=== Select connection ===
1. Dev Server (192.168.1.10 - SalesDB)
2. Prod Server (sql-prod.local - OrderDB)

Enter number: 1

=== Select a query ===
0. Enter query directly
1. sales_report.sql
2. user_list.sql

Enter number: 1

Include header row? (y/n): y

=== Select encoding ===
1. UTF-8
2. UTF-8 with BOM
3. UTF-16 LE
4. Shift-JIS

Enter number: 2

Connecting...
Executing query...
Writing CSV...

Done: C:\Users\you\AppData\Local\Programs\QueryToCsv\output\sales_report_20260302_153045.csv
Rows: 1,234
```

The query list shows the `.sql` files directly in `QueryFolder`, in file-name order.
Select `0` to type SQL instead, and end the input with Ctrl+Z followed by Enter:

```
Enter number: 0

Enter SQL query (end with Ctrl+Z):
  > SELECT TOP 10
  > *
  > FROM Users
  > ^Z
```

### One-Liner Mode

Run a query without prompts, for scripts, scheduled tasks, and quick one-off commands.

```
QueryToCsv --query "SELECT * FROM Users"
QueryToCsv -f sales_report.sql
QueryToCsv -c "Dev Server" --query "SELECT * FROM Users WHERE Active = 1" --no-header -e utf-8-bom
```

| Option | Long | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `-c` | `--connection` | If multiple connections | The only connection | Connection name from appsettings.json, ignoring case |
| - | `--query` | One of `--query`/`--file` | - | Inline SQL string |
| `-f` | `--file` | One of `--query`/`--file` | - | SQL file name (resolved in QueryFolder) or absolute path |
| `-e` | `--encoding` | No | `utf-8` | CSV encoding: `utf-8`, `utf-8-bom`, `utf-16`, `shift-jis` |
| | `--header` | No | (default) | Include header row |
| | `--no-header` | No | - | Exclude header row |

Long options accept both `--name value` and `--name=value`. When an option is repeated,
the last one wins. A relative `-f` path that leaves `QueryFolder` (for example through
`..`) is rejected; give an absolute path instead. The `-q` short name is not accepted
because it is reserved for quiet mode.

### Cancelling

Press **Ctrl+C** to cancel a run at any time: the query is cancelled, no CSV is written,
and the run exits with code 1.

At any input prompt, **Ctrl+Z** followed by Enter also exits (exit code 1). Exception: in
direct SQL input, Ctrl+Z ends the SQL entry and proceeds with execution.

### Help, Version, and This Guide

```
QueryToCsv -h          # or --help: the options in brief
QueryToCsv -V          # or --version: prints "QueryToCsv X.Y.Z"
QueryToCsv --guide     # this guide
```

None of them reads the configuration or writes a log.

### Open Folders and Files

```
QueryToCsv --open queries                # Open the queries folder in Explorer
QueryToCsv --open output                 # Open the output folder in Explorer
QueryToCsv --open config                 # Open appsettings.json in its default editor
QueryToCsv --open log                    # Open the folder log files are written to
QueryToCsv --open "C:\path\to\file.csv"  # Open a file with its default application
```

Target names ignore case. QueryToCsv exits as soon as the target has been opened; a
target that does not exist is reported, with exit code 1.

## CSV Output

### File Naming

Output files are named `{query}_{timestamp}.csv`, for example
`sales_report_20260302_153045.csv`. SQL from `--query` or typed directly has no query
name, so the file is named `{timestamp}.csv`, for example `20260302_153045.csv`.

The timestamp is the machine's local time. If a file with the same name already exists, a
suffix is appended: `_2`, `_3`, and so on. An existing file is never overwritten, and two
runs that finish at the same second both succeed with different names.

The CSV is assembled in `%TEMP%\QueryToCsv` and appears in `OutputFolder` only when
complete, so a reader of `OutputFolder` never sees a partial file, and a network
`OutputFolder` receives one copy. Temporary files a killed run leaves behind are removed
by a later run once they are older than 24 hours.

### Format

- **Standard**: RFC 4180 compliant
- **Quoting**: Fields containing the delimiter, a double quote, a carriage return, or a line feed, or beginning or ending with a space, are enclosed in double quotes
- **Escaping**: Double quotes within fields are escaped as `""`
- **Numbers**: Invariant culture (decimal point `.`, no thousands separator); `decimal` keeps its scale, `float` uses the shortest round-trip form (`1E+20`)
- **Dates**: ISO 8601 (`yyyy-MM-dd` for `date`, `yyyy-MM-ddTHH:mm:ss.fffffff` for `datetime`/`datetime2`, with the offset for `datetimeoffset`) unless `DateFormat` is set
- **Other types**: `bit` as `1`/`0`, binary as `0x` + hexadecimal, `uniqueidentifier` in lowercase; CLR types such as `hierarchyid` must be converted in the query
- **Formula triggers**: text beginning with `=`, `+`, `-`, `@`, a tab, or a carriage return gets a leading `'` so spreadsheets do not evaluate it
- **NULL**: Replaced with the configured `NullValue` (empty string by default)
- **Unrepresentable characters**: a character the chosen encoding cannot hold ends the run naming its row and column; nothing is replaced
- **Empty results**: Outputs header-only CSV (if headers enabled) or an empty file

### Encoding Options

| Option | Description |
|--------|-------------|
| UTF-8 | No BOM. Universal standard |
| UTF-8 with BOM | Recommended when opening CSV in Excel |
| UTF-16 LE | For Windows tool integration |
| Shift-JIS | For Japanese legacy systems |

## Logs

Each run appends to `QueryToCsv_yyyyMMdd.log`, one file per day, in `logs` next to the
executable. When that folder cannot be written to, `logs` under `QueryToCsv` in your local
application data folder is used instead; `QueryToCsv --open log` opens whichever is in
use. At the start of a run, log files are deleted once they fall outside the last
`LogRetentionDays` days, today included; other files in the folder are never touched.

The log records the start and end of each run, every rejected setting and warning, the
connection and query chosen, the file written and its row count, and every error with its
cause. It never contains a connection string or a password.

## Errors and Exit Codes

Every error is one line on standard error beginning with `QueryToCsv: `. A run with
several problems reports only the first one found.

| Scenario | Behavior |
|----------|----------|
| `appsettings.json` missing or unparseable | Reported as a warning; the run continues on the built-in defaults |
| Unusable config value | Reported as a warning naming the setting and the default applied; the run continues |
| No connection configured | `QueryToCsv: Connections must contain at least one entry.`, exit code 1 |
| Connection entry incomplete, duplicated, or unsafe | `QueryToCsv: Connections[<index>]...` naming the problem (missing name or connection string, reused name, invalid connection string, encryption off, user name or password in the string), exit code 1 |
| Connection entry sets `TrustServerCertificate=True` | Reported as a warning naming the entry; the run continues |
| `Credential` not in Windows Credential Manager | `QueryToCsv: credential "<name>" was not found in Windows Credential Manager.`, exit code 1 |
| `QueryFolder` does not exist | Reported as a warning; `queries` next to the executable is used |
| No `.sql` files found | Only "Enter query directly" (option 0) is shown; direct input is still available |
| `OutputFolder` does not exist | Automatically created |
| `OutputFolder` cannot be written | `QueryToCsv: cannot write the output file in "<folder>".`, exit code 1 |
| `-f` relative path leaving `QueryFolder` | Usage error with a `--help` hint, exit code 2 |
| Ctrl+C | `QueryToCsv: cancelled.`, exit code 1 |
| Connection or SQL execution failure | `QueryToCsv: query execution failed.`, exit code 1; the server's error is in the log |
| Query timeout | `QueryToCsv: query timed out.`, exit code 1 |
| Non-SELECT statement detected | `QueryToCsv: only SELECT statements are allowed.`, exit code 1 |
| `--query` and `--file` both specified | Usage error with a `--help` hint, exit code 2 |
| `-c` name not found | Usage error with a `--help` hint, exit code 2 |
| `-c` omitted with multiple connections | Usage error with a `--help` hint, exit code 2 |
| `-f` file not found | `QueryToCsv: SQL file not found: <path>`, exit code 1 |
| Unknown `-e` encoding | Usage error with a `--help` hint, exit code 2 |
| Unknown CLI option or positional argument | Usage error with a `--help` hint, exit code 2 |
| Query returns 0 rows | CSV written (empty or header-only), exit code 0 |

| Exit code | Meaning |
|-----------|---------|
| `0` | Success |
| `1` | Runtime error |
| `2` | Usage error |
