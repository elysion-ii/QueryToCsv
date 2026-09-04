# Shared Engineering Standards (core)

Shared engineering standards, distributed into each repository at `docs/rules/`. This
file is the language-agnostic core. Language-specific rules live in sibling files in the
same directory (`dotnet.md`, ...); application-specific rules live in the application's
rules file listed in the repository's `AGENTS.md` Applications table.

## How to use these rules

Read every rule file in this directory before implementing any change, together with
the rules file and specification of the application being changed — the `AGENTS.md`
Applications table names both. `cli.md` is the only file that may be skipped, and only
while that application exposes no command-line interface (neither a console application
nor a GUI application that accepts command-line options). Judging any other file
unrelated to the work at hand and leaving it unread is how a rule goes unapplied.

- A repository reads the language files matching its technology stack — a .NET
  repository reads `dotnet.md`, a multi-stack repository reads every matching file. Its
  `AGENTS.md` states the stack and the concrete commands (formatter, build, test).
- Each language file begins with an **enforcement matrix** stating which rules the
  project scaffold enforces mechanically (analyzers, build gates) and which must be
  checked by AUDIT.
- Precedence on conflict: the more specific file wins — application rules > language
  rules and `cli.md` > this file. `AGENTS.md` holds no rule text; it only routes.
- Before reporting an implementation task as complete, run **AUDIT** (bottom of this
  file).

---

## Code Design

### Separation of Concerns

- Structure code in layers with distinct responsibilities (e.g., UI → business logic → infrastructure)
- Keep the UI layer thin: event handlers delegate to logic, they do not contain it

### Dependency Direction

- Dependencies must flow in one direction only (e.g., UI → Service → Common → Database)
- Never introduce reverse or circular dependencies. If a change would require one, reconsider the design before implementing

### Functional Core, Imperative Shell

- Separate decisions from I/O: do not make business decisions while reading from DB/files/network — read the data first, then decide on the data already read
- Decision logic takes data as arguments and returns results (pure functions), so it can be tested without infrastructure
- Keep I/O in a thin shell that orchestrates: fetch → decide (pure) → write
- Time, randomness, and environment values are inputs too: pass them as arguments; introduce clock abstractions or DI only when argument-passing is not enough

### Declarative Where It Removes Machinery

Neither form is better by default. A query pipeline is not safer than a loop, and a loop
is not clearer than a pipeline. What decides it is how much the reader has to hold in
their head, and that is visible.

- Write the transformation declaratively when doing so **removes** machinery that carries no meaning: a mutable accumulator, an index, a nesting level, an append call. What remains is the transformation the reader came for — filter, map, group, sort
- Keep the loop when the declarative form **adds** machinery to force the logic into a shape it does not have: a tuple accumulator standing in for mutable state, extra traversals to produce what one traversal already produced, a fold whose body is the loop body with its state spelled out
- The test is not "which is simpler". Simplicity is a judgement about the reader, and the writer is a poor judge of it. The test is whether the rewrite subtracted mechanism or added it, which anyone can check
- Control flow is machinery a declarative form usually adds rather than removes: early exit with a reason, per-item error handling, cancellation checks, state that depends on the previous element. These stay imperative
- Performance justifies keeping the form that reads worse **only with a measurement behind it**. Without one, write the form that removes machinery: an unmeasured belief about speed is not a reason to make code harder to read
- This rule governs the inside of a function. Which side of the boundary code sits on — pure decision or effectful shell — is Functional Core, Imperative Shell above, and it wins where the two meet: the shell is a sequence of steps and stays one

### Snapshot Before Mutate

- Never modify a data source while lazily iterating over it. This applies to any deferred/streaming enumeration in any language: directory listings (e.g., C# `Directory.EnumerateFiles`, Python `os.scandir`, POSIX `readdir`), collections under `foreach`, DB cursors, lazy queries (LINQ, generators)
- Some environments throw immediately; others fail silently — entries get skipped, processed twice, or the loop ends early. Absence of an exception does not mean absence of the bug
- Snapshot first, then mutate: materialize the full item list (e.g., `.ToList()`, `Directory.GetFiles`, `list(...)`, fetch-all) before the loop that modifies the source
- This is the same principle as Functional Core, Imperative Shell applied to iteration: read everything first, then act on the data already read

### No Lasagna Code

A layer that adds no behavior still has to be read, navigated, and kept in step with the
one it forwards to. Layers earn their place by doing something.

- Do not create pass-through delegation methods that just call another service with the same arguments
- When extracting shared logic into a new service, update **all** callers to use the new service directly
- Do not leave wrapper methods on the original class

### Shared Algorithm, Local Data

- When deduplicating similar code, extract only the algorithm (the steps identical everywhere); keep each call site's data (queries, mappings, registrations) local
- If sharing would require flags or parameters that grow with each new caller, stop sharing

### Change as Addition

Adding the tenth variation should cost what the second one cost. It stops costing that as
soon as a variation means editing branches spread across existing code.

- Prefer structures where adding a variation (new screen, format, identifier) means adding a new unit plus a registration, not editing branches across existing code

### Enforce Invariants in Code

- Prefer compile-time impossibility over runtime checks: where practical, express the rule in types (stage-specific interface return types for call order, required constructor arguments, narrowed parameter types) so violations do not compile
- When a rule can be broken silently (wrong call order, configuration after startup, out-of-range values), make the code fail fast with a clear message instead of relying on documentation
- The error message should state the rule itself

### Synchronous vs Asynchronous

- Use async for I/O-bound work (network, DB, file access); keep CPU-bound and pure logic synchronous
- Async propagates end-to-end: never block on async code (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`)
- Do not mark a method async when nothing in it awaits
- Language files refine this per language/framework; they take precedence

---

## Code Comments

### Principle

Comments are generally unnecessary. Prefer clear names, simple structure, and code that explains itself.

Write a comment only when the code cannot express the necessary context by itself. A useful comment explains **why the current shape, condition, guard, or order must be this way now**.

Even a justified comment is capped at a short conclusion (1–2 lines):

- Check Enforce Invariants in Code first — a constraint that types or a fail-fast check can enforce needs neither a comment nor an ADR
- When an ADR covers the context, the comment states only the conclusion plus the ADR path
- Context that does not fit in those two lines is document-sized: its body belongs in an ADR or `docs/references/`, with a one-line conclusion plus the path left at the site. Add it to the document that already covers the subject; a new file is warranted only when the context stands on its own as a decision or a durable fact

Before writing a comment, ask:

- Can a better name, extracted function, type, or test make this clear? If yes, do that instead.
- Is this about what changed, what was removed, who requested it, or how it was aligned? If yes, put it in Git history, a PR, a ticket, or a design document, not in code.
- Can the comment be phrased as "this must be this way because..." in terms of the current behavior? If not, do not write it.

### Good Comments

Good comments explain current constraints that code cannot show:

- Business rules that are not obvious from the code
- External constraints such as API limits, protocol requirements, or legacy system behavior
- Invariants, ordering requirements, and exceptional states
- Non-obvious guards that are required only in specific runtime states
- Why the code has a shape a reader would not have chosen, when a measured result rejected the shape they would have chosen. State the measurement — that is what makes the comment checkable later, and deletable when the measurement stops holding

```csharp
// config is null only on first launch (before the settings file is generated)
if (config != null) { ... }

// The contractual matching key is aaa only; extra conditions drop update history (rationale: docs/adr/0002-order-matching-key.md)
var sql = "SELECT ... FROM [Order_TBL] WHERE aaa = @bbb";

// Three passes over 200k rows cost 400ms; one pass keeps the report inside its 1s budget
var count = 0; var sum = 0m; var max = decimal.MinValue;
```

### Do Not Write

Do not write comments that explain the work history instead of the current constraint:

- What was changed or removed
- Commented-out code, conditions, fields, or parameters
- Notes like "X removed", "X excluded", "changed after review", or "aligned with screen Y"
- Comments that only restate the next line of code
- Bare prohibitions without the current reason
- Performance claims with nothing measured behind them ("for performance", "this is faster") — without the number, the claim cannot be checked, cannot be refuted, and no one dares delete it

If the current code is clear without a comment, write no comment. If the reason matters, explain the reason as a current rule or constraint without preserving removed implementation details.

### Where to Put What

| Information Type | Location |
|-----------------|----------|
| Why the current shape/order/guard is required now | Code comment, only when code cannot express it |
| External constraints, business rules, invariants | Code comment |
| What was changed or removed | Git history / ChangeLog / Release notes |
| Why code was changed | Git commit message / PR description |
| Why something is prohibited | ADR (`docs/adr/`), plus a brief current reason at the usage site if needed |
| Context needing more than 1–2 lines to explain | ADR (`docs/adr/`) or `docs/references/`, with a one-line conclusion + path comment at the site |
| Implementation decisions | ADR (`docs/adr/`) or Docs (`docs/`) |
| Author's work process (review fixes, what was aligned with) | Nowhere in code — that is what PR / commit history is for |

---

## Testing

### Environment Independence

- Tests must not depend on the execution environment: OS culture/locale, timezone, machine state, pre-existing database contents, or network availability
- When behavior depends on culture or time, pin them explicitly inside the test

### Self-Sufficient Test Data

- Each test creates the minimal data it needs (e.g., via test data builders with sensible defaults)
- Do not depend on shared fixture databases or data prepared outside the test — shared mutable test data rots; per-test data keeps tests independent

### Table-Driven Tests

- When a function's spec is "input → output", write tests as a specification table (parameterized tests), not as near-identical separate test methods
- Order rows consistently: null/none → empty → minimal → main cases → boundaries
- Make input values self-describing so the table reads as documentation

---

## Specifications

- **Spec-first**: when a change requires behavior not in the specification (`docs/specs/...`, listed in the `AGENTS.md` Applications table), update the specification before implementing. Never retro-fit the spec to the implementation afterwards
- What a specification must contain and how it is structured is defined in `documentation.md`

---

## Version Display

The first thing to establish about a reported problem is which build produced it. An
application that cannot state its own version turns that into guesswork, and every answer
after it is built on a guess.

- Every application must expose the version it was built with, in the form `<AppName> X.Y.Z` (e.g., `MyApp 1.22.3`)
- `<AppName>` is the application's own name, spelled as the application spells it — the GUI and the CLI print the identical string
- GUI applications display it somewhere always reachable in the UI (title bar, about screen, footer)
- CLI applications provide `--version` / `-V`; the concrete requirements are in `cli.md`
- The default format is the bare product version — no build hash, build date, or other metadata
- The displayed value comes from the project's single version definition (see the language file's version-management rules), never from a hardcoded string

---

## Configuration Values

Applies to every value the application reads from outside its own code — configuration
files, environment variables, and any other external store.

- **Every configurable value has a built-in default in code**, and the application runs on that default whenever the external value is missing, unparsable, or out of range. A configuration source that is absent or corrupt never fails startup
- **Validate on read**: replace or clamp invalid values while the configuration is being loaded, so no later code has to defend against them. The valid range belongs to the value's definition, not to checks scattered across its use sites
- **Report every substitution once**, naming the value, what was rejected, and the default applied. A silent fallback hides operator mistakes and turns a typo into an invisible change of behavior
- **Externalizing a value is a choice**: a value nobody adjusts in the field stays a named constant in code (see the language file's constants rule). Making it configurable adds the obligations above
- This does not relax Enforce Invariants in Code — that rule governs values the program itself produces, where a violation is a defect and must fail fast. An external value is operator input: the program keeps running on a known-good value and reports what it rejected
- Precedence between sources, for applications that expose a command line, is in `cli.md` (CONFIG)

### Configuration Files in the Repository

Applies to every configuration file that ships with the application — application
settings, deployment descriptors, workflow definitions, and the like.

- **The repository tracks a placeholder template, never a working configuration.** The tracked file is `<name>.template.<ext>` and carries placeholder values only; the file the application actually reads is derived from it and stays untracked. A working configuration holds the credentials, host names, and paths of whoever wrote it — committing one leaks them and makes every other environment inherit them
- **A developer's own configuration is local to that developer.** Derive it from the template, keep it out of version control, and never treat it as a source of truth for anyone else
- **What ships is derived from the template by the build**, so the distributed file is placeholder-clean by construction. Distribution never picks up a configuration file that happened to be sitting in a working tree
- **Installation never overwrites an existing configuration.** The derived file is written only when none is present, so updating an installation keeps what the operator configured
- The template is a real, complete configuration in shape: every key the application reads appears in it, so the file doubles as the documentation of what is configurable

---

## Procedure Design

Applies to any ordered sequence of steps written for someone — human or agent — to
execute: guides (`docs/guides/`), release and migration steps, setup instructions, and
step sequences delivered in a PR description or conversation. Where procedure documents
are placed is `documentation.md`'s concern; this section governs how the steps
themselves are designed.

### Design Before Writing

- Do not accrete steps ad hoc. Fix the entry state and the goal state first, then design
  the path between them before writing any step
- When the design stops holding mid-way, do not patch around it with extra steps —
  return to the design, reorder, then rewrite

### Phases and Steps

- Structure any procedure beyond a few steps in two layers: phases (headings) and the
  steps inside them. One phase = one concern; one step = one operation
- The phase sequence alone must tell the whole story (e.g., prepare → change → verify →
  clean up)
- Do not split for splitting's sake: work that forms one responsibility stays in one
  step, and a short procedure needs no phases — the same judgment as not creating a
  folder for a single file

### Motivated Order

- Adjacency is the only grouping device a document has. Code expresses relatedness
  through folders and namespaces regardless of distance; a linear document cannot —
  operations of the same kind scattered across distant steps lose their structure
- Keep same-kind operations adjacent (e.g., one "update configuration" phase covering
  app A, then app B) unless a dependency or verification requirement forces separation —
  "change A, verify A, only then change B" is a valid reason to separate
- Every step's position must be explainable by dependency, verification, or locality.
  A step that could move elsewhere without breaking the procedure has no reason to be
  where it is — regroup it

### Step Contract

- A step may rely only on explicitly stated inputs or the outputs of earlier steps —
  no implicit preconditions. Reading top to bottom must never require looking ahead
- Every step ends with a verifiable confirmation: not "done" but an observable check
  ("the command exits 0", "the service responds on port 8080")
- For substantial procedures, write each step as precondition / operation / confirmation

### Name the Place

- Steps adjacent in text implicitly claim "you are still in the same place". When that
  claim is false, the reader hunts for a control that does not exist in front of them,
  with no way to tell "wrong place" from "not found yet" — never leave the location
  implicit
- Every step names the place it operates in (URL, screen, full path), stated so the
  reader can verify "am I here?" before looking for the operation's target. On the
  named place with the item absent, the document is wrong — stop searching, fix the
  document
- Moving between places is itself an operation: when consecutive steps happen in
  different places, write the transition as an explicit step
- The named place is a claim about the external system, not about the document — verify
  it against the actual structure (the real UI hierarchy, the real directory tree)
  instead of assuming continuity from the previous step

### Automation Boundary

- Do not leave automatable work as manual steps out of inertia; fold it into existing
  automation (build scripts, batch files) where one exists
- Keep operations that require human judgment, and irreversible operations, as manual
  steps, explicitly marked as such ("requires confirmation", "irreversible"). Do not
  over-integrate

---

## AUDIT

Run this procedure after completing an implementation task and **before reporting it as
complete**. Do not report completion until AUDIT passes.

### 1. Reload the rules

- Re-read the root `AGENTS.md`, and any nested `AGENTS.md` covering the directories you touched
- Re-read this file, every applicable language file in this directory, and the rules files of the applications you touched (skim is enough if already read this session and unchanged)

### 2. Identify target files

Git changes determine which **files** are targets — not which lines.

| Input | Targets |
|-------|---------|
| (none specified) | All uncommitted changes: `git status --porcelain` (modified, added, untracked) |
| A file path | Only that file |
| A folder path | Files in that folder only |

Check ALL target files. Do not skip any. Do not sample.

### 3. Check file contents

Once a file is a target, **every line in that file** is subject to all rules — do not
distinguish between changed and pre-existing lines; a violation is a violation regardless
of when it was introduced. Check each target file against:

- The core principles in this file
- Every language-file rule whose enforcement matrix row says **AUDIT** (skip rules marked as mechanically enforced — the build gate covers them; but if the repository lacks the enforcing mechanism, treat those rules as AUDIT items too)
- The application-specific rules from the application's rules file (and any other repository-authored files under `docs/rules/`)
- The application's specification (`docs/specs/...`): implemented behavior must match it, and any needed spec change must have happened before the implementation, never as an after-the-fact edit
- If the task created, changed, moved, renamed, archived, or deleted any document, also check it against `documentation.md`; if the task performed a Git write or PR operation, also check it against `git.md`

### 4. Run the mechanical gates

Run the commands defined in `AGENTS.md`: the formatter in check mode, the build
(zero warnings), and the tests. All must pass.

### 5. Fix and report

Fix violations and re-run from step 2. Report completion only when clean, including:

- Files checked
- Violations found and fixed (file / line / rule), or "no violations"
- Gate results with exit codes
- Anything deliberately left unresolved, stated explicitly
