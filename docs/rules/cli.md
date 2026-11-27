# CLI Standards

**Applies when** the application exposes a command-line interface to users or scripts — a console application, or a GUI or background application that accepts command-line options. Does not apply to reusable libraries, or to applications whose host framework parses internal runtime arguments without exposing user- or script-facing CLI options or commands.

Defines the stack-agnostic ideals for command structure, option and subcommand naming,
help output, stream separation, exit codes, error reporting, configuration precedence,
and version flags, based on GNU-style long options and subcommands. This file defines
standards for CLI capabilities when present rather than requiring unrequested features;
only `--version` / `-V` is mandatory on every CLI application.

## Command Structure

- **Consistent invocation order**: accept commands in the order `app [global-options] <command> [command-options] [arguments]`, placing global options directly after the program name, subcommand options after the subcommand name, and positional targets (e.g., file paths) last, keeping global flags unambiguously separated from subcommand flags and target arguments.

## Option Form

- **Canonical long options**: give every option a lowercase hyphen-separated long form (`--name`, e.g., `--dry-run`) without uppercase letters or underscores, accepting both `--output file.txt` and `--output=file.txt`, keeping scripts self-documenting and consistent with GNU-style option parsing.
- **Single option prefix style**: accept only `--name` and `-x` option forms; do not accept platform-specific prefixes such as `/silent`, which collide with POSIX absolute paths across cross-platform shells.
- **Short forms only for frequent options**: assign single-letter short forms (`-x`) only to routinely typed options, supporting bundling for valueless flags (`-abc`) and space-separated values (`-o file.txt`), conserving the finite single-letter flag space for common operations.
- **End-of-options separator**: treat all tokens following `--` strictly as positional arguments without further option parsing so filenames or values starting with `-` can be passed unambiguously.

## Reserved and Conventional Options

The table below reserves option names: when the corresponding feature exists, use its reserved name and short form. Only `--version` (`-V`) is mandatory; other entries apply only when their feature is implemented.

| Long form | Short | Behavior | Requirement |
|---|---|---|---|
| `--version` | `-V` | Print the application version and exit | Mandatory |
| `--help` | `-h` | Print help and exit | Mandatory when help is provided (providing help itself is recommended) |
| `--verbose` | `-v` | Enable detailed output | Only with verbose-output feature |
| `--quiet` | `-q` | Suppress non-warning output | Only with output-suppression feature |

- **Preserve `-v` / `-V` distinction**: reserve `-v` exclusively for verbose output and `-V` exclusively for version output, preventing cross-tool muscle-memory errors when checking versions.
- **Last flag wins between `--verbose` and `--quiet`**: when both options exist and appear on the same invocation, apply whichever appeared last on the command line, allowing callers to override preset flags in shell aliases or wrapper scripts.

### Conventional Feature Options

When implementing any feature below, use its conventional option name rather than inventing synonyms:

| Option | Purpose |
|---|---|
| `--output <path>` / `-o` | Output destination |
| `--config <path>` / `-c` | Configuration file path |
| `--force` / `-f` | Skip interactive confirmation |
| `--dry-run` | Preview actions without altering durable state |
| `--format <name>` | Select output representation (`json`, `text`, ...) |
| `--no-color` | Disable colored terminal output |

## Version Output

- **Print the version and exit**: `--version` and `-V` print the application's version to standard output and exit 0 (specializing where the version appears under `standard.md` Version Display for CLI applications, so callers and scripts can query a tool's version uniformly without executing work).

## Subcommands

- **Use subcommands only for distinct operations**: split into subcommands when the application performs multiple independent operations; keep single-operation CLIs flat without subcommands, avoiding redundant wrapper verbs on single-purpose tools.
- **Shallow verb or target-noun hierarchy**: name subcommands in lowercase as verbs (e.g., `build`, `show`, `set`) or target nouns (e.g., `config`), nested at most two levels deep (e.g., `app config set`), keeping command paths predictable and discoverable without deep command trees.
- **Uniform naming across subcommands**: use consistent verbs for CRUD operations across the CLI (e.g., `list`, `show`, `set`/`add`, `remove`), and use identical option names for identical meanings across every subcommand (never `--output` on one subcommand and `--out` on another), so callers do not have to memorize per-subcommand synonyms.

## Help Output

Applies when help is implemented, except the zero-argument rule, which applies to every CLI.

- **Per-level help**: support `--help` at the root and at every subcommand level (`app --help`, `app config --help`, `app config set --help`).
- **Required help content**: include (1) a one-line summary, (2) a usage syntax line (e.g., `Usage: app [options] <target>`), (3) the subcommand list when subcommands exist, (4) option short/long forms with descriptions, and (5) representative examples whenever examples convey more than the usage line alone.
- **Meaningless zero-argument invocation**: when invoking the CLI with no arguments performs no valid default behavior (unlike a GUI application launching its window), print only the brief usage syntax line plus a pointer to `--help` (when implemented) on standard error — omitting the `<app>: <message>` error prefix (`Error Messages` below) because the usage synopsis itself already names the command — and exit with code 2, rather than printing full help, avoiding flooding the terminal with full help text on a mis-invocation while still signaling a usage error to callers.

## Standard Streams

- **Separate data from diagnostics**: write primary machine- or user-consumable results exclusively to standard output, and write logs, progress indicators, warnings, and errors exclusively to standard error.
- **Clean structured output**: when `--format json` (or another machine-readable structured format) is selected, emit nothing on standard output outside that valid structured payload.

## Exit Codes

| Code | Meaning |
|---|---|
| 0 | Complete success (every requested item succeeded) |
| 1 | Runtime error (an operation failed) |
| 2 | Usage error (invalid option, missing argument, or invalid invocation syntax) |

- **No silent partial success on exit 0**: exit 0 only when the entire requested operation succeeded; partial completion (e.g., skipped or failed items) exits 1 or a documented code 3 or higher.
- **Document custom exit codes**: whenever exit codes 3 or higher are used, document each code in `--help` or application documentation.

## Error Messages

- **Consistent error prefix**: write CLI errors to standard error prefixed with `<app>: <message>` so callers, shell pipelines, and CI logs can identify which command emitted each diagnostic line when multiple tools run together.
- **Usage error hint**: on invalid options or syntax errors, follow the error line with a pointer to `--help` (e.g., `Try 'app --help' for more information.`) when help is implemented so callers can immediately discover valid syntax; suggesting the closest matching flag is encouraged though optional.

## CLI Configuration Precedence

- **Strict source precedence**: resolve overlapping settings in descending precedence: command-line options > environment variables > configuration files > built-in defaults (refining `configuration.md` when present).
- **Consistent environment variable names**: when the CLI defines application-specific environment variables (not required unless the application supports environment-variable configuration), derive names from an uppercased application prefix followed by upper snake case (e.g., `APP_OUTPUT`, allowing framework section delimiters such as `__` where required) and document the mapping to CLI options in help or documentation — prefixing avoids variable collisions with other tools in the process environment, and upper snake case stays portable across POSIX and Windows shells (established cross-tool conventions such as `NO_COLOR` remain unprefixed).

## Compatibility

- **Replace renames cleanly**: renaming a published option or subcommand, or changing an exit-code meaning, breaks the scripts that call it, so do so only when unavoidable; when an option or subcommand must be renamed, accept that break and replace the old name outright rather than maintaining compatibility aliases that inflate tests and clutter help (overriding the general external compatibility exception in `standard.md` Leave No Vestiges).
- **Machine-readable stability contract**: guarantee stable output structure only under explicit structured formats such as `--format json`; treat human-oriented plain-text output as non-contractual.
