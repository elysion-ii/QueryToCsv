---
status: active
created: 2026-11-27
---

# QueryToCsv Deferred Violations

Code and documentation in QueryToCsv that do not yet meet a rule under `docs/rules/`, and
whose fix was deferred (`docs/rules/standard.md`, AUDIT step 5). Violations that involve
a decision of the application are recorded in
`docs/references/QueryToCsv/violating-behavior.md` instead. Each entry is removed in the
change that fixes it.

## No boundary tests across the database or the CSV file

- **Where**: `QueryToCsv.Tests/`; the untested code is `QueryToCsv/QueryExecutor.cs` (`ExecuteAsync`, `WriteCsvAsync`, `FormatValueAsync`)
- **Rule**: `docs/rules/testing.md`, Boundary Tests (exercise the real database query and the real file format against instances the test run creates)
- **Current state**: query execution and CSV writing have no test. QueryToCsv-008 and the CSV-writing side of QueryToCsv-010 in `docs/specs/QueryToCsv.md` are verified by nothing.
- **Fix**: run a SQL Server instance for the test run in Docker on WSL, built only from schema the repository defines. Test query execution against it, and read the written CSV back with a CSV parser to check quoting, NULL, numbers, dates, delimiter, line terminator, encoding, and BOM.
- **Waiting on**: the Docker and WSL environment, which is being set up for these tests

## No end-to-end tests

- **Where**: the repository; `AGENTS.md` lists the end-to-end test command as "none yet"
- **Rule**: `docs/rules/testing.md`, Test Layers (establish an end-to-end layer once behavior is externally observable) and End-to-End Tests
- **Current state**: nothing launches the built executable. Exit codes, the standard output and standard error contract, the written file, and the log are not verified from outside the process.
- **Fix**: launch the built `QueryToCsv.exe` from a test-owned directory with a test `appsettings.json` pointing at the test SQL Server. Drive the one-liner mode, and the interactive mode through redirected standard input. Assert the exit code, both output streams, the CSV at its final path, and the log entries. Add the command to `AGENTS.md`.
- **Waiting on**: the same Docker and WSL environment

## Requirement IDs, test references, and ledgers of the specification

- **Where**: `docs/specs/QueryToCsv.md`; `QueryToCsv.Tests/`
- **Rules**: `docs/rules/documentation.md`, Requirement IDs (`<PREFIX>-<number>` with an uppercase prefix); `docs/rules/testing.md`, Requirement Coverage; `docs/rules/specs.md`
- **Current state**: requirement IDs are written `QueryToCsv-001`, which the ID syntax does not allow, so the requirement-coverage check in `build/Build.ps1` finds no requirement and verifies nothing. No test names a requirement ID. The specification also does not yet answer every question `specs.md` asks of each decision, and has no unconfirmed-behavior ledger.
- **Fix**: run `/specify-app`, which renames the IDs, adds the `[Trait("Requirement", ...)]` references and the tests each requirement needs, and writes the missing answers and the ledger
