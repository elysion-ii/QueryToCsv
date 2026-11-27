# Specifications

Shared rules defining what a specification states about every decision of an application,
where each answer comes from, and how existing implementation behavior lacking an answer
is classified and tracked. A decision left unspecified is settled silently by whoever
writes the code; formal defects such as unmatched branches, check-then-act races, failed
reads interpreted as negative matches, and unverified boundary values arise from unstated
decisions. How a specification is verified against this file (e.g., model checking,
solver, case enumeration, or review) is the application's choice.

Specification structure, requirement text criteria, and requirement IDs are governed by
`documentation.md`; spec-first timing by `standard.md` (Specifications); test coverage of
requirements by `testing.md` (Requirement Coverage).

## Scope

- **Decisions added or modified by a change**: any change that introduces a decision or alters a decision's inputs, classes, or outcomes satisfies this file for that decision.
- **Complete coverage when specifying from existing code**: when writing a specification from an existing implementation, satisfy this file for every decision in the application.
- **Existing untouched decisions wait**: decisions predating adoption of this file that no change has touched remain outside scope until specified from code or modified by a change.
- **Uniform criterion before and after code**: the questions below stay identical whether authoring specifications before implementation or reconstructing them from code; only the permitted answer sources differ (Where Answers Come From).

## Decisions

- **Observable decision definition**: a point where an externally observable outcome varies based on any value the application reads. Externally observable means solely: what the application displays, what it returns or exits with, what it writes outside its process (e.g., a file, database, message, or external system), or whether it accepts, rejects, or performs an operation. Diagnostic log entries are not externally observable outcomes unless a requirement of the specification names them (`standard.md` Functional Core, Imperative Shell).
- **Non-decisions**: internal choices whose alternatives yield identical observable outcomes (e.g., two equivalent calculation algorithms) and constant values that never alter an observable outcome are not decisions.
- **One decision per outcome**: all conditions collectively selecting among the alternatives of one observable outcome (e.g., whether an order is accepted) form one decision regardless of how many code locations evaluate them.
- **Decision inputs**: every value whose variation can change the decision's outcome — e.g., user or external system inputs, configuration values, stored state, current time, and outcomes of other operations.

## Questions

For each decision in scope, the specification answers every question below whose condition
holds for that decision.

- **Answered by requirements**: a question is answered when specification requirements (`documentation.md` Requirement IDs) state what the question asks, regardless of which specification section holds them.
- **Answered or recorded**: when documenting an existing implementation, a question is also accounted for by an entry in an application behavior ledger (Ledgers). The entry does not answer the question: it keeps the question open (unconfirmed) or the code deferred (violation) until an answer source (Where Answers Come From) settles it.
- **Classes state conditions, not examples**: specify outcome classes via value conditions (e.g., "quantity from 1 to 999"), never by sample values alone; concrete input → output pairs belong in test code (`documentation.md` Document Responsibilities).
- **Heading IDs name questions**: the uppercase identifier before each question heading (`OUTCOMES`, `SETTINGS`, ...) is used in ledger entries and reviews to reference that question.

### Conditions and Settings

#### OUTCOMES: Every value has one outcome

Inputs falling outside enumerated cases reach arbitrary branches, and dead branches go unnoticed.

- **Applies to**: every decision.
- **Complete input class partitioning**: name all decision inputs and partition each into classes covering every value the input can carry at entry — including invalid, missing, empty, zero, and negative values wherever possible. A successfully read source containing no value (e.g., an empty field or absent optional key) is classified here rather than under UNAVAILABLE.
- **Deterministic outcome per combination**: every class and combination of input classes maps to exactly one outcome; where two classes overlap, explicitly state precedence. Broad rules ("regardless of X") satisfy multiple combinations at once.
- **Exact ordered boundaries**: ordered boundaries (numbers, lengths, dates, times) state the boundary value, inclusivity/exclusivity, and unit — and for dates or times, the governing time zone or offset.
- **Ordered outputs**: sequence outcomes (e.g., listed rows, exported records, emitted messages) state their sort order or explicitly state that order is unspecified.
- **Constraints from other rules**: e.g., `standard.md` (External Input — Reject, never repair) and, when present, `configuration.md` (Configuration Values — Validate once on read).

#### SETTINGS: Settings that empty or override each other

Combined settings can cancel every option out or leave one setting silently overridden by another.

- **Applies when**: the decision evaluates two or more pre-configured settings (e.g., configuration values, filter criteria, schedules, limits).
- **Empty-intersection combinations**: for any combination of settings under which no input can reach an outcome (e.g., an entity both included and excluded, or start after end), state whether the combination is rejected on configuration entry or accepted with a defined outcome.
- **Setting precedence**: for every pair of settings where one value renders another ineffective, state which setting wins.
- **Constraints from other rules**: e.g., when present, `configuration.md` (Configuration Values — Validate once on read).

#### FORMATS: Values in an earlier representation

Persisted data, settings, and messages often outlive the version that wrote them; reading legacy formats under current assumptions changes meaning silently.

- **Applies when**: an input can arrive in a format or schema representation older than the current writer produces (e.g., legacy config files, rows written before a schema change, older partner payloads).
- **Explicit mapping or rejection**: state, for every supported earlier representation, how each legacy value maps to current values to produce equivalent semantics — or state that the earlier representation is rejected and define the rejection behavior.
- **Constraints from other rules**: e.g., when `db.md` applies, `db.md` (Schema Changes and Migrations — Convert existing rows on semantic change).

### Concurrency and Time

#### SHARED: State that other operations change at the same time

Separating a check from its write allows concurrent operations to both pass the check against stale state and violate limits without error.

- **Applies when**: the decision reads state that the operation modifies or bounds (e.g., inventory, balance, quota, uniqueness) — whether stored locally or in another system — and another request, user, process, or instance can alter or decide against that state before the first operation's effect is recorded.
- **Excluded only by statement**: exempted only when the specification explicitly states that no concurrent operation modifies that state; that statement itself requires a valid answer source (Where Answers Come From).
- **Named invariant**: state the invariant condition the shared state always satisfies under a clear name (e.g., `STOCK: item stock is never negative`).
- **Concurrent conflict outcome**: specify which operation succeeds and what competing operations observe (e.g., rejected with conflict error) while preserving the named invariant.
- **Admitted means counted**: every admitted operation is counted toward the constrained state where that state lives immediately upon admission, preventing subsequent decisions from overlooking queued or deferred updates.
- **Constraints from other rules**: e.g., `standard.md` (Writes That Hold Together — Detect concurrent updates) and, when `db.md` applies, `db.md` (Database Constraints).

#### STALE: Data older than the state it stands for

Reading a lagging copy allows decisions to break invariants without concurrency.

- **Applies when**: the decision reads a copy of state owned elsewhere (e.g., read replica, cache, exported snapshot, cached startup settings, previously fetched partner response).
- **Maximum staleness**: specify either that live authoritative state is required or the maximum tolerated age of the copy and the outcome when that age is exceeded.
- **Invariants hold up to max age**: every named invariant (SHARED) remains satisfied when acting on copies up to the permitted maximum age.

#### PARTIAL: Reading while another writes

A reader intersecting an incomplete write sees fractured state that never validly existed.

- **Applies when**: the decision writes state readable concurrently or after an unexpected stop mid-write (e.g., by another process, thread, external system, or the application itself after restart).
- **Named atomic units**: define which values together form one indivisible unit.
- **All-or-nothing visibility**: readers observe each unit wholly before or wholly after the write — never half-written — including after mid-write interruption.
- **Constraints from other rules**: e.g., `standard.md` (Writes That Hold Together — Atomic units).

#### REPEAT: The same intent arriving more than once

Retries, duplicate deliveries, and repeated submissions execute the same action twice unless identified as one intent.

- **Applies when**: a state-changing operation can be triggered repeatedly for the same intent (e.g., timeout retry, duplicate message delivery, double submission, crash re-run).
- **Intent identity**: specify what identifies repeated arrivals as the same intent (e.g., a client-assigned idempotency key reused across retries; per-attempt IDs identify nothing).
- **Idempotent replay**: subsequent arrivals for an already processed intent leave durable state unchanged and return the original outcome so senders observe the true result.
- **Distinguish from business uniqueness**: a rule limiting distinct intents (e.g., one coupon per user) must not block re-deliveries of an already admitted intent.
- **Constraints from other rules**: e.g., `standard.md` (Writes That Hold Together — Safe to re-run).

#### LOOPS: Repetition that must end

Retry or polling loops bounded only by an external response or optional caller deadline run forever when that peer hangs.

- **Applies when**: the application repeats an action until a condition holds (e.g., retries, polling, waiting on a resource, progressive backoff).
- **Self-enforced bound**: specify a maximum iteration count or elapsed duration measured by the application itself and the outcome upon exhaustion; external conditions or optional caller timeouts are never the sole termination bound.

#### PROPAGATION: Changes that must reach another place

Fire-and-forget emission after a commit drops updates if the process stops between committing and sending.

- **Applies when**: a committed change must subsequently reach another destination (e.g., external system, derived index, replica, notification).
- **State invariant and deadline**: express propagation as a target-to-source consistency condition (e.g., every shipped order appears in the carrier manifest) and state the maximum time after commit before it holds.
- **Crash recovery**: guarantee the condition holds within that deadline even if the application stops at any instant after committing and later restarts.
- **Constraints from other rules**: e.g., `standard.md` (Writes That Hold Together — Atomic units, Safe to re-run).

### Boundaries and Trust

#### UNAVAILABLE: Inputs that cannot be obtained

Treating read failures as "false" or "no match" flips behavior across negations and causes combined guards to disagree.

- **Applies when**: obtaining an input or evaluating a predicate on it can fail (e.g., unreachable source, missing resource, error response, timeout) so no value exists to classify. Successfully obtained empty or malformed values fall under OUTCOMES instead.
- **Explicit failure class**: specify the outcome when an input is unavailable separately from outcomes for obtained values, even when both lead to the same visible action.
- **Post-combination visibility**: state the externally visible outcome after all boolean combinations (negation, any-of, all-of) over the failed input.
- **Consistent multi-guard outcome**: when multiple checks can halt the same operation, specify the combined outcome when any check's input becomes unavailable.
- **Constraints from other rules**: e.g., `standard.md` (Failure Handling and Reporting) and, when present, `configuration.md` (Configuration Values — Installation settings fail fast at startup).

#### EMPTY: Empty sets

Vacuous truth turns empty universal conditions into "match everything" and empty existential conditions into "match nothing".

- **Applies when**: a decision operates over a set that can be empty (e.g., zero rules/filters, empty allow/deny list, empty search result, zero rows).
- **Explicit empty outcome**: state the externally observable outcome for an empty set explicitly, never leaving it to emerge implicitly from logical quantifiers or loop defaults.

#### ORIGIN: Who can set each input

Trusting caller-controlled fields or client-only checks lets callers forge identities, roles, prices, or limits.

- **Applies to**: every input of every decision.
- **Declared origin**: state the source of each input and which actor can modify it.
- **Trusted verification of protected effects**: an untrusted input may determine protected attributes (e.g., caller identity, permissions, pricing, limits) only after an explicit server-side or trusted-boundary check; checks running inside caller-controlled environments (e.g., browsers, client apps) do not qualify.
- **Constraints from other rules**: e.g., `standard.md` (External Input — Validate at the boundary; Values Never Become Code).

#### REPRESENTATION: Values crossing a boundary

Mismatched units, precision, time zones, encodings, or code meanings silently corrupt data across boundaries without raising parse errors.

- **Applies when**: the decision exchanges or persists values across an external or version boundary (e.g., files, databases, APIs, messages).
- **Both sides specified**: state unit, precision, time zone or UTC offset, character encoding, code-value definitions, and numeric range on both sides of the boundary where applicable.
- **Out-of-capacity handling**: specify what happens when a value valid on one side exceeds precision, range, or defined codes on the other.
- **Constraints from other rules**: e.g., `standard.md` (External Input — Canonicalize, then validate) and, when present, `values.md` (Time and Dates; Machine-Readable Formatting; Text Length and Equivalence).

## Where Answers Come From

Every answer originates from one of three sources; an unstated answer invented silently by an implementer is an unmade decision.

- **Shared, stack, and application rules**: files under `docs/rules/`. Where rules leave only one compliant answer, that answer enters the specification; where rules permit multiple options, the user chooses.
- **The user**: the decision maker defining what the application must do — expressed via the triggering request, explicit answers to questions, or existing specification requirements.
- **Existing code (candidate only)**: when reconstructing specifications from existing code, what the code currently does (including its comments, tests, and guards) is strictly a candidate answer until classified below — never authoritative on its own.
- **Ask unanswered questions**: ask the user whenever an applicable question is unanswered by rules or existing user decisions; before implementation exists, never write code before obtaining that answer.

## Classifying an Implementation's Behavior

Applies when deriving specifications from existing code across every applicable question of every decision. Classify each code answer strictly into the first matching category (only these three exist):

1. **Violation**: code behavior breaks a rule under `docs/rules/`, contradicts an existing specification requirement, or conflicts with a user answer.
2. **Specified**: rules permit only the code's behavior, or the user explicitly confirms it.
3. **Unconfirmed**: all other observed code behaviors, including ambiguity reading the code alone cannot resolve.

Comments, tests, or guards inside the code never elevate an answer out of Unconfirmed: they show developer intent rather than user decision.

## Handling Each Class

- **Specified → requirement**: state specified answers as specification requirements (`documentation.md` Requirement IDs) with matching test coverage (`testing.md`).
- **Violation → user decides fix or defer**: never fix a violation unilaterally without user approval.
- **Fixed violation**: update the specification to state the compliant rule/user answer as a requirement and fix the code in the same change.
- **Deferred violation**: record it in the violation ledger with the target compliant behavior (or permissible rule choices if undecided); keep current code unchanged and never state violating behavior as a specification requirement.
- **Contradicted existing requirement**: when code conflicts with an existing spec statement, the user may update the spec to match code (making it Specified); if deferred instead, move that requirement out of the specification into the violation ledger so the specification never makes false claims about current code.
- **Unconfirmed → ask user**: present to the user; if confirmed, promote to Specified; if rejected in favor of another behavior, classify the current code as a Violation.
- **Unanswered → record in unconfirmed ledger**: until the user responds, leave code unchanged, keep it out of the specification, and record it in the unconfirmed ledger.
- **Resolving ledger entries**: any change altering behavior recorded in either ledger resolves that ledger entry in the same change (via user answer for Unconfirmed, or code fix for Violation).

## Ledgers

Ledgers record actual implementation behaviors deliberately excluded from the specification; as implementation facts rather than desired behavior, they reside under `docs/references/` beside the specification (`documentation.md` Document Responsibilities).

- **Ledger paths**: for application identifier `<App>` (from `AGENTS.md` Applications), place the unconfirmed ledger at `docs/references/<App>/unconfirmed-behavior.md` and the violation ledger at `docs/references/<App>/violating-behavior.md`. Migrate any pre-existing single-file `docs/references/<App>.md` into `docs/references/<App>/` (`documentation.md` Application Scope).
- **Read alongside specification**: read both ledgers whenever reading `<App>`'s specification (before changes and in AUDIT step 1 of `standard.md`).
- **Entry structure**: record one entry per `(decision, question ID)` naming the decision, question heading ID, observed code behavior, code location (file and symbol), and whether comments, tests, or guards assert it.
- **Additional unconfirmed fields**: include the exact question for the user and the options rules allow.
- **Additional violation fields**: cite the violated rule or spec requirement and the target compliant fix (or allowed rule options).
- **Retire when resolved**: remove resolved entries in the resolving change, and delete any ledger file left empty.
