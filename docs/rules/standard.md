# Shared Engineering Standards (Core)

Stack-agnostic engineering ideals applied to every application. Distributed under
`docs/rules/` alongside any stack rule files and conditional rule files that apply to
the repository, and the repository's own application rules.

## How to Use These Rules

Rules in `docs/rules/` form three tiers:

- **Application rules**: repository- and application-specific rules and overrides, written in the repository's own files under `docs/rules/` and routed to from `AGENTS.md`
- **Stack rules**: technology-stack means and constraints distributed for the repository's stack
- **Shared rules**: universal rules (`standard.md`, `documentation.md`, `specs.md`, `testing.md`), plus any conditional shared rule files distributed to the repository

- **Precedence on conflict**: Application rules > Stack rules > Shared rules.
- **No conflicts among shared rules**: a contradiction between two stack-agnostic rule files under `docs/rules/` is a defect in the rules. Report it to the user and continue only on the user's decision — never by silently picking one side.
- **Rule scope**: every change complies with all four universal rule files under `docs/rules/` (`standard.md`, `documentation.md`, `specs.md`, `testing.md`), with every conditional or stack rule file present under `docs/rules/` whose `Applies when` condition (if any) holds for the application being changed after the change — evaluated as a fact settled by the application's code, never whether the current task touches that subject (ignoring files whose condition is false for that application, such as in multi-application repositories) — and with the specification under `docs/specs/` of every touched application.
- **Missing conditional rules**: conditional shared rule files cover ten stack-agnostic domains, each named here by the opening of its `Applies when` condition — `cli.md` (exposes a command-line interface to users or scripts), `client.md` (is packaged, distributed to, and run directly on user or operator machines), `cloud.md` (stores objects or files in cloud storage, integrates with cloud-provider managed services, or maintains cloud-hosted backups), `configuration.md` (reads persistent or environment-supplied settings), `db.md` (reads or writes a database), `file-exchange.md` (reads or writes files exchanged with people or other programs), `remote-calls.md` (invokes external systems over a network), `security.md` (handles secrets or personal data, or restricts data or actions per authenticated user or role), `values.md` (persists domain values or exchanges them across system, file, or format boundaries), and `web-api.md` (exposes an HTTP or Web API). When a change introduces or expands behavior into any domain above whose rule file is absent from `docs/rules/`, report the missing file(s) to the user before or alongside implementation so they can be added to `docs/rules/` (never invent obligations from memory for an absent file; `Rule scope` binds the change only to files present under `docs/rules/`).
- **Required reading**: read every applicable file under `docs/rules/` before implementing any change.
- **Completion requirement**: no implementation task (any change that adds, modifies, or deletes source code, tests, build configuration, specifications, or documentation) is reported complete until the AUDIT gate at the end of this file passes.

---

## Code Design

### Separation of Concerns

- **Split by feature first**: keep the code that changes together for one feature (its screens, screen logic, decisions, and data access) together, apart from other features (`Code Organization`).
- **Separate decisions from I/O inside a feature**: within a feature, decision code stays apart from presentation and external I/O (`Functional Core, Imperative Shell`), so each part changes for one reason only.
- **Delegate from UI handlers**: where a UI exists, UI event handlers forward input and invoke logic without embedding business decisions.
- **Keep presentation decisions testable**: where a UI exists, compute choices among visible alternatives (e.g., color, enabled or visible state, status message) derived from data or operation outcomes in code a test can invoke without rendering a screen; fixed display constants require no decision code.

### Dependency Direction

- **One-way dependencies**: dependencies flow in one direction only and never form a cycle (e.g., screen code calls decision code; decision code depends on no I/O code). Redesign any change that would introduce a reverse or circular dependency before implementing it.
- **No required inversion**: inverting dependencies so that decision code defines interfaces for I/O code to implement is not required; it is used only where a current requirement needs the substitution (`Build for Current Requirements`).

### Build for Current Requirements

- **No provisions for unrequested change**: never add provisions for substitutions or extensions no current requirement needs (e.g., an interface with a single implementation, an unused extension point, a generalization with a single caller). An interface whose only second implementation is a test substitute is allowed (`No Lasagna Code`). `Minimal Visibility`, `One Home per Piece of Knowledge` (promote when the second consumer appears), and `No Lasagna Code` apply this rule to their own subjects. A provision another rule requires (e.g., a cancellation parameter on every asynchronous I/O operation, `Synchronous vs Asynchronous`) is a current requirement, not a speculative one.

### Minimal Visibility

A declaration visible wider than its callers claims callers that do not exist and invites new code to depend on it.

- **Narrowest enclosing scope**: declare every type, module, and member at the narrowest visibility that covers all existing callers; widen visibility only when a caller that requires it is written.
- **Match enclosing reach**: a member is never declared more visible than its enclosing type or module allows, except where an external caller or framework requires that wider declaration.
- **Frameworks count as callers without widening defaults**: frameworks that invoke or bind code by runtime rules (e.g., serializers, dependency-injection containers, UI binders, code generators) count as callers for the visibility they require, but never configure a framework to bypass its default visibility rules and expose non-public members broadly.
- **Tests never widen visibility**: tests reach internal code solely through the stack's test-access mechanism or through public entry points — never by widening production declarations for test access.
- Stack rules define the language's modifiers, framework requirements, and test-access mechanisms.

### Functional Core, Imperative Shell

- **Separate decisions from I/O**: fetch data in an outer I/O shell, execute pure decisions on that in-memory data, and write results back in the shell — never interleave decisions with live reads or writes across databases, files, or networks.
- **Pure decision functions**: decision logic accepts data as arguments and returns results without external side effects, allowing tests to run without infrastructure.
- **Side effects stay in the shell**: decision logic may emit diagnostic log entries (which alter no domain state or result); every persistent or external write remains in the imperative shell.
- **Pass ambient values as inputs**: pass current time, randomness, and environment values (e.g., locale or host settings) as arguments; introduce a clock or provider abstraction only when a single argument cannot represent changing values (e.g., measuring elapsed duration) or a framework mandates a fixed signature.

### Declared Mutation

A call changes only what its name and signature declare, and state mutates only where mutation is required.

- **No hidden argument mutation**: modify an argument passed to a function only when the function's name declares that mutation (e.g., sorting or filling in place) or the language signature marks the parameter as mutable; return new values otherwise.
- **Immutable by default**: mark variables, fields, and objects unassignable or immutable wherever the language supports it, unless subsequent mutation is required.
- **Framework-constructed objects**: leave state mutable only where a framework sets properties solely after construction; where a framework can populate immutable members during construction, configure it to do so.

### Declarative Where It Removes Machinery

Inside a function, prefer whichever form leaves less control-flow machinery to trace while preserving readability.

- **Declarative pipelines**: use filter, map, group, sort, and aggregate pipelines when they eliminate mutable accumulators, manual index tracking, nested loops, or manual list building.
- **Imperative loops**: use explicit loops when a pipeline would require state-mimicking tuple accumulators, redundant passes, or complex control flow (e.g., early exits with reasons, per-item error recovery, cancellation checks, dependencies between adjacent items).
- **Measure before complicating**: justify a harder-to-read optimization solely with profiler measurements, never with an assumption.

### Snapshot Before Mutate

- **Materialize before mutating**: never modify a data source (e.g., a directory listing, collection, database cursor, or lazy iterator) while lazily iterating over it, because concurrent modification skips entries, repeats items, or fails. Collect the items to change first — the whole list, or one bounded chunk at a time where the whole list is too large to hold in memory — then apply changes to that snapshot before reading further.

### No Lasagna Code

Every layer must justify its existence by adding distinct behavior, transformation, or substitution.

- **No pass-through wrappers**: eliminate methods or types whose callers receive the identical result by calling the wrapped target with the identical arguments; cross-cutting concerns such as logging or metrics belong in the layer doing the work or a shared cross-cutting mechanism.
- **Adapters add substitution**: an interface implementation whose sole purpose is substituting an external dependency during testing is a valid boundary adapter, not a pass-through wrapper.
- **Call extracted logic directly**: when extracting logic into a new component, update every caller to invoke it directly and remove any leftover forwarding wrapper.

### One Home per Piece of Knowledge

Duplicating a business rule, calculation, or mechanism causes one copy to drift stale when changes arrive; conversely, merging code that only looks alike couples independent reasons for change.

- **Search before writing**: before adding a function, type, or constant, look up the code map (`documentation.md` Code Map) for the feature folder or mechanism home that holds the same knowledge, and reuse or adapt that code (within Shared Algorithm, Local Data) instead of duplicating it.
- **Keep the code map current**: a change that adds, moves, or removes a feature folder or the home of a mechanism shared across features (e.g., database connection, logging, configuration loading, test infrastructure such as test-store setup) updates the code map in the same change.
- **One home per mechanism**: how the application performs an operational concern (e.g., building a connection, resolving application directories, writing logs, reaching an external system) lives in one place regardless of caller count; callers obtain the result there and pass caller-specific parameters (e.g., a longer timeout for migrations) rather than assembling a second copy.
- **Share domain knowledge inside its boundary**: keep single-domain knowledge within that domain until a second domain actually needs it; promote across domains only when the second consumer appears.
- **Group by reason to change, not surface similarity**: merge two fragments only when a single external authority (e.g., a law, contract, or single decision owner) would change both together; keep distinct policies separate even when their formulas happen to match today.
- **Library calls alone are not duplicated knowledge**: calling a standard platform API directly (e.g., reading a built-in property or creating an isolated test fixture) is shared shape rather than duplicated application knowledge.

### Shared Algorithm, Local Data

- **Extract invariant steps only**: share only the algorithmic skeleton common across callers, letting each call site supply its own data, queries, mappings, or rules.
- **Stop sharing when flags proliferate**: do not unify code if unification requires mode flags or special-case parameters that grow with each new caller.

### Change as Addition

- **Single branch point per variation**: branch on a set of variations (e.g., screens, formats, handlers) in exactly one dispatch or registration point so adding a new variation updates one location rather than scattering edits.

### Leave No Vestiges

Code reads as if the current design were the only one ever written.

- **Delete superseded code completely**: remove unused implementations, unreachable branches, obsolete flags, dead fields/parameters, and commented-out code; rely on version control history rather than dead remnants.
- **No relational naming**: name replacements canonically rather than relative to predecessors (e.g., `_old`, `_new`, `V2`, `Legacy`, `Ex`); version tokens belong only in externally published interface identifiers.
- **External compatibility exception**: retain a legacy path only while external consumers outside the repository (e.g., published APIs, persisted formats, wire protocols) still depend on it — except where a domain rule expressly forbids compatibility aliases (such as published CLI options or subcommands under `cli.md`, when present); update all in-repository callers in the same change.
- **Document retained compatibility**: attach a comment at any retained external compatibility path naming the external dependent and the exact condition that retires it.

### Enforce Invariants in Code

- **Make invalid states unrepresentable**: encode ordering, required arguments, and value constraints in types or constructors wherever the language's type system supports it so invalid usage fails at compile time or construction time rather than downstream.
- **Validate at every entry to a constrained type**: enforce the invariant in every constructor, mutator, and default path so no invalid instance can exist.
- **Trust validated types downstream**: code receiving an invariant-enforcing type relies on the invariant without redundant defensive checks or assertions.
- **Fail fast on silent program contract breaks**: verify programmatic state-machine order, post-initialization immutability, and out-of-range internal arguments immediately with an error naming the violated rule. (Settings read at startup follow `configuration.md` when present.)

### Named Constants

- **Name non-evident or synchronized literals**: extract a literal into a named constant in its owning module or class (rather than a catch-all constants file) whenever its meaning is not self-evident at the use site or when multiple sites must keep holding the same value. Keep self-evident literals (e.g., `0`, `1`, empty string) inline, and treat field-adjustable values as configuration (`configuration.md`, when present).

### Resource and Lifetime Discipline

- **Release owned resources deterministically**: close or dispose every owned external or unmanaged resource (e.g., file handles, streams, sockets, database connections, locks) on every exit path, including failures.
- **Unsubscribe shorter-lived listeners**: detach event handlers, callbacks, and observers when the subscribing component's lifetime ends if the publisher can outlive it, preventing retained references and memory leaks.
- **Resource use stays bounded whatever the input size**: processing whose memory, duration, or parameter count grows with the size or number of its inputs, and state kept across operations that grows with the inputs seen (e.g., a cache that keeps every key it has read), is bounded — by a limit on the input, beyond which the input is rejected; by processing the input in pieces (e.g., streaming, chunks, pages); or, for kept state, by a limit on its entries or their age. A limit is a named value, and the outcome beyond it is stated in the specification.

### Writes That Hold Together

Applies to every write to durable state (e.g., databases, files, outgoing messages).

- **Atomic units**: group writes that are valid only together (e.g., header and line items, file and index record) into a single all-or-nothing transaction or atomic replace (e.g., write temporary file and rename) so partial failure leaves no inconsistent state.
- **Safe to re-run**: operations interrupted midway by crashes, timeouts, or duplicate triggers produce the same valid end state when run again, because re-runners cannot tell how far a prior attempt got.
- **Detect concurrent updates**: where concurrent actors can modify the same state, verify at write time that the underlying version or state read earlier has not changed, and report conflicts rather than silently overwriting earlier writes.

### Synchronous vs Asynchronous

- **Async I/O, sync compute**: perform I/O-bound work asynchronously wherever the platform provides asynchronous APIs; keep CPU-bound and pure decision logic synchronous.
- **Propagate async end-to-end**: never block synchronously on asynchronous work (e.g., waiting synchronously on an async handle or future), which risks deadlocks and thread exhaustion.
- **No empty async**: never mark a function asynchronous unless it awaits genuine asynchronous work.
- **Propagate cancellation end-to-end**: pass cancellation signals through all cancellable I/O operations so callers can abort work cleanly; omit cancellation **only** for the four non-cancellable completion steps — logging/telemetry, transaction rollback, best-effort cleanup, and post-commit finalization whose interruption would leave committed state inconsistent.
- Stack rules refine concurrency primitives and signatures per stack and take precedence.

---

## Code Organization

Applies to how source files, namespaces or modules, folders, and compilation units are structured.

- **Predictable file-to-symbol mapping**: name source files after the cohesive type, module, or feature unit they define (following the stack's convention — e.g., one top-level type per file in C#/Java, or cohesive feature modules in languages where related small types share a file) so every symbol is located directly from filenames without searching.
- **Logical modules match folder layout**: align each file's namespace, package, or module path with its directory path relative to the project root so logical names and filesystem paths never diverge.
- **Organize folders by business feature**: when a project creates subdirectories (instead of keeping a single flat root folder), group code that changes together for one business area or feature into that feature's folder rather than splitting files across top-level technical-layer folders (e.g., `ViewModels/`, `Services/`, `Repositories/`), and avoid duplicating layer-named subfolders inside feature folders unless a compiler or deployment boundary requires them; place cross-cutting mechanisms in their own shared home (`One Home per Piece of Knowledge`). *Overlap with Separation of Concerns and Dependency Direction*: `Separation of Concerns` and `Dependency Direction` govern responsibility boundaries and dependency direction between types, whereas feature organization governs which folders hold those types.

---

## Code Comments

A comment exists only where clearer names, types, extracted functions, tests, or fail-fast checks cannot express the context — it tells a blank-slate reader **why the current code shape, order, or guard is required now**.

- **Explain current non-obvious constraints**: comment only on what code cannot show on its own (e.g., business rules, external API or protocol quirks, state-specific runtime invariants/ordering guards, or performance-driven shapes citing concrete measurements so the optimization stays verifiable and removable when conditions change).
- **One constraint at the site; offload deep context**: state one constraint and its current reason directly at the code it shapes; move architectural rationale, prohibited alternatives, or multi-constraint specifications to an ADR (`docs/adr/`) or `docs/references/`, leaving a one-line conclusion and relative path in the comment.
- **No restatements, history, or unmotivated claims**: never write comments that restate what code visibly does, commented-out code, change or review history (record what changed and why in commits, PR descriptions, or changelogs), prohibitions without a current reason, or unmeasured performance claims (e.g., "for performance", "faster").

---

## Specifications

- **Spec-first**: update an application's specification (`docs/specs/...`, listed in `AGENTS.md` Applications) before implementing behavior it does not yet cover — never write it after implementing that behavior (specifying existing behavior from code follows `specs.md` Scope). Specification content and structure are governed by `documentation.md`; decision completeness is governed by `specs.md`.

---

## Version Display

Establishing which release produced a behavior or failure comes first in diagnostics and support.

- **Users can learn the version**: every standalone executable application (e.g., GUI, CLI, network service, or background worker; reusable libraries without their own executable entry point are exempt) gives its users a way to learn the release they are running (e.g., an About screen, a `--version` option, a startup log line). How the version is written and where it appears are left to the application, subject to any applicable conditional rule file (e.g., `cli.md` Version Output when present and applicable).
- **Single version definition**: define the application's release version in one place in the project so every built artifact, package, and displayed version derives from that single source without drifting out of sync; fail fast rather than substituting a hardcoded fallback constant if version metadata is absent.

---

## Application-Owned Paths

- **Anchor application-owned paths explicitly**: for every application-owned path — any path the application selects or constructs for its own files (e.g., configuration, local data, logs, bundled assets), as opposed to target paths supplied by a caller via CLI arguments, UI file pickers, or request payloads — resolve it relative to an explicit base the application controls (only one of: the application's executable directory, a platform-designated application-data directory, an absolute path from configuration, or — solely for transient per-operation temporary files — a platform-designated temporary directory; the specific location is decided by the specification), never relative to the process's current working directory, which varies by shortcut, scheduler, service manager, or parent process. (Where configuration supplies a relative path, either anchor it to an explicit base above — using the temporary directory solely for transient files — or handle it as an invalid configuration value per `configuration.md` Configuration Values when present, or reject it under `External Input` when `configuration.md` is absent; user-supplied relative CLI arguments intentionally resolve against the caller's working directory.)
- **Keep persistent paths deterministic and stable**: place persistent application-owned files (e.g., configuration, user preferences, local data, logs) at explicit, deterministic paths controlled directly by the application (matching the specification) — never nested under runtime- or framework-generated non-deterministic or version-keyed directories (e.g., executable-location hashes, randomized provider folders, or per-version subdirectories), which silently disconnect updates or relocations from existing state, leave orphaned folders behind, and prevent operators from locating or backing up data. (Where the specification places portable persistent files beside the executable directory, those files intentionally travel with the application folder rather than depending on an external location hash. Transient per-operation temporary files — e.g., staging files under `file-exchange.md` when present, atomic-write temporary files, or isolated test scratch directories under `testing.md` — and unique item identifiers defined by the specification, such as content-addressed cache keys or uploaded object filenames, are outside this prohibition.)

---

## External Input

Applies to all data entering across an external trust boundary at run time — i.e., inputs received across a user interface, command line, file, external process, or network boundary rather than constructed by already-validated internal code. Settings read from configuration sources follow `configuration.md` when present, or the boundary validation rules below when `configuration.md` is absent; a setting a user enters through the application's own UI is input checked here when entered.

- **Validate at the boundary**: check input size, shape, format, and range once where data enters the application (size limits per `Resource and Lifetime Discipline`), passing only validated types inward so inner code never repeats boundary checks.
- **Canonicalize, then validate**: before checking, apply only the canonicalizations defined by the format's specification (e.g., trimming outer whitespace, normalizing line endings, Unicode normalization) and permitted formatting variants (e.g., grouping separators) — never guess intended values for malformed data.
- **Reject, never repair**: reject invalid input (whole payload or failing independent items, per specification) with an error stating what failed, rather than silently altering invalid values into valid-looking ones nobody submitted.

---

## Values Never Become Code

- **Pass values as data**: never concatenate external, configured, or stored values into executable or interpreted text (e.g., SQL, shell commands, HTML, templates, query expressions); always use parameterized bindings, argument lists, or context-aware encoding.
- **Whitelist fixed structural choices**: where a language position has no data-binding mechanism (e.g., SQL table or column names, sort keys), select only from a fixed set of code-defined alternatives and reject any other input.
- **Forbid type-polymorphic deserialization of untrusted data**: never pass untrusted or external payloads into deserializers that embed type metadata to instantiate arbitrary types during parsing; deserialize strictly into explicit, statically known data shapes (`External Input`).

---

## Failure Handling and Reporting

- **Never swallow failures**: handle, recover explicitly, or report every caught failure — never ignore an exception or error silently.
- **Never expose raw internal errors**: translate internal exceptions into safe user-facing messages rather than surfacing internal details (e.g., raw exception messages, stack traces, internal file paths such as installation or staging paths, or connection strings) in messages shown to end users; diagnostics for operators follow `Log each failure once`.
- **Log each failure once**: the boundary that handles a failure without propagating it records the log entry; intermediate layers that rethrow or propagate never log it again.
- **Unattended failures reach a person**: background jobs, services, and scheduled tasks report failures to an actively monitored channel (e.g., alert, monitored event log, scheduler failure exit code), never solely into a local file nobody checks.
- **Attended failures guide the user**: interactive operations tell the user what failed and what action they can take next, never ending silently or crashing without explanation.

---

## Build Hygiene and Static Analysis

- **Consistent automated formatting**: maintain formatting settings in repository-level style configuration without ad-hoc file-level deviations, and verify formatting with a non-modifying check before completing work.
- **Recommended static analysis with zero warnings**: run the toolchain's standard recommended static analysis and treat warnings as errors so defects fail checks immediately; never lower or disable analysis gates globally — suppress an individual diagnostic only at the narrowest applicable scope (e.g., single line or statement, single declaration, generated file or folder, or single compilation unit) with a documented current constraint (never "for now" or "fix later").
- **Centralized prohibited-API enforcement**: when the repository enforces a banned-API list, define prohibited APIs in one repository-level list without folder exemptions so forbidden calls fail consistently; remove an API from the list rather than exempting individual callers if any legitimate use exists.
- **Keep generated build outputs out of version control**: exclude compiler, bundler, and packager build outputs from version control in every repository regardless of whether it ships distributables, preventing stale or environment-specific artifacts from entering repository history.
- **Known-vulnerable dependencies are found**: check third-party dependencies against vulnerability advisories during builds or on a regular maintenance schedule, and resolve critical or high-severity disclosures promptly.
- **Gated distributable builds**: where the application produces distributable artifacts, produce them solely through the repository's gated build pipeline (after format verification and tests pass) rather than ad-hoc manual packaging.

---

## AUDIT

Run this procedure after completing every implementation task (`Completion requirement` above). Do not report a task complete until AUDIT passes.

1. **Reload the rules**: re-read the root `AGENTS.md`, any nested `AGENTS.md` covering touched paths, every applicable file under `docs/rules/`, the specification (plus any behavior ledgers per `specs.md`) of every touched application, and any existing deferred-violation notes under `docs/notes/` covering touched scopes. Files already read in full during the same session and unchanged since need not be re-read.
2. **Identify target files**: determine targets from uncommitted version-control changes (modified, added, untracked), or from an explicitly requested file or folder. Inspect the entire content of every target file — finding the violations already present in a touched file as well as those the change introduces — and sample or skip no target file.
3. **Verify target files against rules and specifications**:
   - **Shared rules in every target file**: zero violations introduced by the change across every universal file and every conditional rule file whose `Applies when` condition holds for the touched application after the change (evaluated as a fact about the application, never whether the task touches that subject); violations already present before the change are handled in step 5.
   - **Missing conditional domain coverage**: check whether the change introduces or expands behavior in any conditional shared domain listed under `How to Use These Rules` (`Missing conditional rules`) whose rule file is absent from `docs/rules/`; report any missing file(s) to the user in step 5.
   - **No duplicated knowledge repository-wide**: inspect every added function, type, or constant against the entire repository (`One Home per Piece of Knowledge`).
   - **Stack rules in every target file**: zero violations of every rule marked `AUDIT` in any applicable stack rule file's enforcement matrix (when present), and of any `Enforced` rule whose mechanical gate is missing from the repository, introduced by the change.
   - **Application rules in every target file**: zero violations of repository-authored rules under `docs/rules/` introduced by the change.
   - **Specification alignment**: implemented behavior matches the application specification under `docs/specs/`, with specification updates committed before or alongside implementation — excluding untouched legacy decisions outside `specs.md` scope (`specs.md` Scope) and pre-existing behavior currently tracked in `docs/references/<App>/unconfirmed-behavior.md` or `docs/references/<App>/violating-behavior.md` unless the change alters that behavior.
4. **Run mechanical gates**: execute every verification command listed in `AGENTS.md` (format verification, zero-warning build, unit and boundary tests — omitting any not applicable to the repository) and confirm exit code 0. End-to-end tests follow the schedule in `testing.md`.
5. **Fix and report**: fix every violation the change introduced, repeating from step 2 until none remains. Report the violations already present in target files without fixing them — mixing their repair into the change hides the change's own behavior in review — and fix one only when the user decides to (for decision violations, per `specs.md` Handling Each Class). Record the ones the user defers: decision violations in `docs/references/<App>/violating-behavior.md` per `specs.md` (moving any contradicted requirement out of `docs/specs/`), and other code or documentation violations under `docs/notes/` (e.g., `docs/notes/<App>/deferred-violations.md` or `docs/notes/deferred-violations.md`). Report completion listing the files checked, any missing conditional rule files, violations found and fixed (or "no violations"), mechanical gate outcomes with exit codes, and any user-deferred items.
