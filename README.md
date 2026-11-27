# QueryToCsv

A CLI tool that connects to Microsoft SQL Server, executes `.sql` files, and exports the results as CSV.

## Features

- Interactive query selection from a folder of `.sql` files, or direct SQL input from the console
- One-liner mode for scripts and scheduled tasks
- Streaming execution via `SqlDataReader` (constant memory usage regardless of result size)
- RFC 4180 compliant CSV output (powered by CsvHelper)
- Configurable delimiter, null representation, newline, and date format
- Choice of CSV encoding: UTF-8, UTF-8 with BOM, UTF-16 LE, Shift-JIS
- Optional header row
- SELECT-only enforcement: the query is parsed as T-SQL, and anything but a single SELECT statement is rejected before it reaches the server
- SQL Server credentials kept in Windows Credential Manager, never in the configuration file
- Logging to file with daily rotation and configurable retention
- Self-contained single-file executable (.NET runtime not required), with its usage guide built in

## Requirements

- Windows x64
- Microsoft SQL Server (any supported version)
- .NET 10.0 SDK (for building from source)

## Download

Pre-built binaries are available on the [Releases](https://github.com/elysion-ii/QueryToCsv/releases/latest) page.

| File | Description |
|------|-------------|
| `QueryToCsv-Setup-*.exe` | Installer (creates folders, optional PATH registration) |
| `QueryToCsv.exe` | Standalone executable |

## Quick Start

1. Install with `QueryToCsv-Setup-*.exe`, which installs per user under
   `%LOCALAPPDATA%\Programs\QueryToCsv` and creates `appsettings.json`, `queries`, and
   `output` there. Choose the option that adds the folder to your `PATH` to run
   `QueryToCsv` from any directory, as the commands below do.
2. Open the configuration with `QueryToCsv --open config` and define a connection:

   ```json
   {
     "Connections": [
       { "Name": "Local", "ConnectionString": "Server=myserver;Database=mydb;Integrated Security=True;Encrypt=Mandatory;" }
     ]
   }
   ```

3. Put a `.sql` file holding one SELECT statement in the `queries` folder
   (`QueryToCsv --open queries`).
4. Run `QueryToCsv` and answer the prompts, or run a query directly:

   ```
   QueryToCsv -f sales_report.sql
   QueryToCsv --query "SELECT TOP 10 * FROM Users"
   ```

   The CSV lands in the `output` folder (`QueryToCsv --open output`).

## Usage Guide

The [usage guide](docs/guides/usage.md) covers everything else: SQL Server
authentication through Windows Credential Manager, encryption and server certificates,
every setting, SQL file rules, interactive and one-liner mode, the CSV format and file
naming, logs, and every error with its exit code.

The same guide is built into the executable, so it is at hand on any machine that has
QueryToCsv:

```
QueryToCsv --guide
QueryToCsv --guide > usage.md
```

`QueryToCsv --help` lists the options in brief.

## Building from Source

Run `build/Menu.bat` and select **Build** from the menu. This produces a self-contained executable in `build/QueryToCsv/`, with `appsettings.json` created from `QueryToCsv/appsettings.template.json`. The build runs a configuration-file check, a code format check, a requirement-coverage check, and the test suite first, and stops if any of them fails.

## Building the Installer

Requires [Inno Setup 6.3 or later](https://jrsoftware.org/isinfo.php).

1. Run `build/Menu.bat`
2. Select **Full Build**

The installer:
- Installs to `%LOCALAPPDATA%\Programs\QueryToCsv` (per-user, no admin required)
- Creates `queries/` and `output/` folders
- Optionally adds the install directory to user `PATH`
- Preserves `appsettings.json` on upgrades (only created on first install)
- On uninstall, removes the program and keeps `appsettings.json`, `queries/`, `output/`, and `logs/`
- In an unattended upgrade or uninstall, asks a running QueryToCsv to close and fails without changing anything when it does not

## License

MIT License. See [LICENSE](LICENSE) for details.
