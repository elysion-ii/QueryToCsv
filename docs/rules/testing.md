# Testing

Shared ideals governing test layers, environment independence, test data, structure,
change coverage, boundary and end-to-end testing, requirement traceability, and
execution timing. Stack rules and individual repositories select the concrete test
frameworks, runners, and environment provisioning mechanisms.

## Test Layers

Assign each test to the first layer in the table below whose condition matches what the code under test executes through during the test.

| Layer | What the code under test executes through | Example |
|---|---|---|
| End-to-end | An external entry point or execution boundary of the built application running in its own process (e.g., a UI window, CLI entry point, network API, or background job/watched-input trigger) | Driving a UI window through automation; invoking the built executable with arguments; triggering a built service against a test store |
| Boundary | A real database, partner-readable file format, or network protocol (only these three) backed by an instance created for the test run | Running a query against a test-built database; writing a file and parsing it back |
| Unit | Direct in-process calls to code only | Invoking a pure decision function with a table of input cases |

- **Explicit command per layer**: `AGENTS.md` defines the command running each test layer the application contains; unit and boundary tests may share one command, while end-to-end tests use a separate command not executed by the unit/boundary command.
- **Add end-to-end tests once behavior is externally observable**: establish an end-to-end layer as soon as the specification includes any requirement observable through an external entry point or execution boundary (e.g., UI display, stdout/exit code, API response, exported file, or durable/outbound effect of a background job); artifacts lacking external entry points or execution boundaries (e.g., reusable libraries) need no end-to-end layer.
- **Keep exhaustive input cases in unit (or boundary) tests**: test every decision's input partitions at the unit layer — or the boundary layer when the decision executes inside an external boundary such as a database query — while reserving end-to-end tests for verifying that end-to-end wiring works rather than exhausting every input branch.

## Environment Independence

- **No dependence on ambient uncontrolled state**: tests never depend on state outside the test run's control (anything the test run or the repository's setup prerequisites did not establish) — e.g., machine locale/culture, wall-clock time, time zone, ambient environment variables, external files, pre-existing database contents, or third-party networks — even if current behavior happens not to vary. Scratch directories created by the test run and locally started or configured test services/databases are under test control.
- **Inject environment inputs explicitly**: supply time, locale, and configuration explicitly — directly as arguments in unit/boundary tests, or via launched-process configuration/environment in end-to-end tests. Where an end-to-end test cannot control an ambient input, verify behavior dependent on that input in unit or boundary tests instead, and assert only ambient-independent outcomes in end-to-end tests.
- **Never call external third-party services**: tests never contact live external services (including free sandboxes) whose availability, state, and rate limits lie outside test control; substitute boundary interfaces with in-process doubles in unit tests, and point the code or built application at locally started test instances or local wire-protocol test stubs/servers created by the test run in boundary and end-to-end tests.

## Self-Sufficient Test Data

- **Construct minimal local data**: each test creates only the minimal data needed to exercise its behavior (e.g., via builders with defaults).
- **No shared mutable fixtures**: tests never rely on shared mutable data prepared manually or mutated across tests; committed read-only fixture files alongside tests are permitted when treated strictly as immutable test inputs.
- **Isolate unkeyed state**: whenever one test's data could affect another test regardless of keys (e.g., operations scanning whole tables), run that test against its own isolated store or database.
- **Seed baseline data through production paths**: baseline installation data (e.g., lookup rows, seed records, version markers) is populated in test environments by running the same installer, migrator, or setup script used in production — never copied from a running instance.

## Table-Driven Tests

- **Parameterize input-to-output checks**: express specifications mapping inputs to outputs as parameterized data tables rather than duplicating near-identical test methods.
- **Systematic row order**: order test rows consistently from degenerate and minimal inputs through representative main cases to boundary and invalid cases (e.g., null/none → empty → minimal valid → representative main cases → boundary and invalid cases) so missing edge cases stand out at a glance.
- **Self-describing cases**: write inputs and case labels clearly so the test table itself documents the specification.

## Tests Accompany the Change

- **Test new or modified decisions**: every change adding a decision or altering its conditions, thresholds, or outcomes includes tests pinning the changed outcomes so regressions are caught automatically.
- **Reproduce defects first**: every defect fix adds an automated test failing before the fix and passing after it so the bug cannot regress silently.
- **Test new or modified requirements**: every change adding or editing a specification requirement adds its verifying tests (`Requirement Coverage`).
- **Retrofitting IDs brings tests**: adding a requirement ID to an existing un-IDed requirement brings full verifying coverage and decision input-case tests in the same change.

## Boundary Tests

- **Exercise the real boundary**: code speaking to a database query, serialized partner file format, or wire protocol must be tested across that real boundary using test-created instances (e.g., querying a real migrated test database, round-tripping serialized output through a parser); mocking the boundary leaves query syntax, schema alignment, and serialization unverified. A test at any layer (including end-to-end) traversing that real boundary satisfies this rule.
- **Build test stores from repository definitions**: construct test databases solely from the repository's own migrations or schema definitions, never from copied backups or live environments.

## End-to-End Tests

- **Drive the built artifact from outside**: launch the actual built application in its own process and interact solely through its external entry points or execution boundaries, with zero test-only code paths, conditional branches, or test-only settings inside the application (copying build outputs into an isolated test directory with test configuration still exercises the built application).
- **Isolated test-owned environment**: point the application via configuration (`configuration.md`, when present) or command-line options exclusively at databases, directories, and stub services created by the test run.
- **Assert the durable outcome**: verify where the effect landed (stored row, output file, visible UI transition), not solely an acknowledgment message; inspecting output files or test database state directly is permitted.
- **Condition-based waits**: wait for an observable state condition (window visible, row written, port open) up to an explicit timeout — never fixed sleep delays.
- **Parallel-safe test-owned data**: use test-unique identifiers or isolated environments so tests execute safely in any order without cross-test interference; tests may seed and clean up their own isolated records directly.
- **Clean up completely**: terminate all spawned processes and delete created test environments when the run finishes, including on failure or abort.

## Requirement Coverage

Requirement IDs are defined in `documentation.md` (Requirement IDs).

- **Link tests to requirement IDs**: reference the verified requirement ID in the test name, metadata attribute, or an adjacent comment directly above the test, following the stack rule file's supported convention.
- **A verifying test fails if behavior breaks**: a test counts as verifying a requirement only when it names the ID and would fail if the production behavior fulfilling that requirement were removed or bypassed (e.g., an end-to-end UI total check verifies a "display order total" requirement whereas testing math alone does not; testing `10.125` verifies half-up rounding whereas `10.10` leaves rounding unexercised; testing multiple rows verifies listing all records whereas one row would pass a `first()` bug).
- **Partial supporting tests may also reference the ID**: unit tests covering sub-cases behind a requirement may reference the requirement ID alongside the full verifying test.
- **Verify every IDed requirement**: every specification requirement carrying an ID has at least one verifying test; requirements observable via an external entry point or execution boundary have at least one end-to-end verifying test, unless dependent on an input an end-to-end test cannot control (`Environment Independence`), in which case a unit or boundary test verifies it.
- **No dangling test references**: no test may cite a requirement ID absent from active specifications; removing a requirement removes all test references to its ID.
- **Un-IDed legacy requirements**: requirements not yet assigned IDs (`documentation.md` Requirement IDs) remain exempt until assigned IDs.

## When Tests Run

- **Unit and boundary tests**: run at the AUDIT gate of every implementation task (`standard.md` AUDIT) and before any distributable artifact is produced.
- **End-to-end tests**: run the full end-to-end suite before opening a pull request and again after every subsequent push to that pull request (or before pushing directly when a repository's rules permit landing changes without a pull request); individual implementation-task AUDIT gates need not run end-to-end tests.
