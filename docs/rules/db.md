# Database Standards

**Applies when** the application reads or writes a database — any data store queried through a database engine, whether a standalone server, managed cloud service, or embedded local database file. Does not apply to applications storing data solely in memory, in plain non-database files (`file-exchange.md`, when present), or behind external HTTP/RPC APIs whose database the application never queries directly (`remote-calls.md`, when present).

Defines stack-agnostic ideals for schema ownership, schema-enforced invariants,
migrations, safe concurrent rollouts, query shape, parameter handling, transactions, and
database access control. Durable atomicity, idempotency, and optimistic concurrency ideals
common to all stores live in `standard.md` (Writes That Hold Together); boundary tests
against repository-built database instances live in `testing.md` (Boundary Tests).

## Schema Ownership

- **One owning repository per schema**: assign every database schema (database, or cohesive set of tables, views, constraints, and indexes managed together) to exactly one owning repository, and permit schema changes only from that owner's migrations.
- **Non-owners treat the schema as external**: an application accessing a schema it does not own never modifies that schema; required schema changes are delivered through the owner's repository, and the consuming application documents its dependency in its specification (`specs.md`).

## Database Constraints

- **Enforce structural invariants in the schema**: declare uniqueness, referential integrity (foreign keys), non-null requirements, and domain check constraints directly in the database schema rather than relying solely on application checks, which concurrent writes, other applications, or bugs can bypass.
- **Application pre-checks are for user feedback only**: an application may perform prior validation checks to produce clearer messages, while keeping schema constraints as the authoritative guarantee and handling constraint rejections cleanly.

## Schema Changes and Migrations

- **Versioned migration scripts only**: deliver every schema modification as migration scripts committed to the owning repository rather than manual edits via interactive database tools, so changes stay reviewable and reproducible across environments.
- **Deterministic apply order**: encode the complete execution sequence in repository filenames or metadata (e.g., monotonic sequence numbers or timestamps) rather than relying on memory or manual lists.
- **Reference data belongs in migrations**: insert and update mandatory baseline rows required for operation (e.g., lookup or code tables) through the same versioned migration pipeline as structural changes.
- **Record applied migrations in the database**: persist the set of applied migrations inside the target database as part of applying them, determining pending migrations by querying that record rather than manual tracking.
- **Forward-only once shared**: from the moment a migration is pushed to any shared repository or environment (including any migration with unknown distribution history), never edit or delete it; revert mistakes solely by adding a new compensating migration.
- **Convert existing rows on semantic change**: when a migration changes the meaning, unit, split, or code mapping of existing columns (`specs.md` FORMATS), migrate existing stored rows to the new meaning within that same migration so old and new meanings never coexist silently.

## Deploying Schema Changes

- **Keep migrations compatible with concurrent code versions**: maintain compatibility with both the previously running code version and the incoming code version while a rollout overlaps, because instances and clients rarely switch instantaneously.
- **Expand before contracting**: split column or table renames and removals across separate releases — first add the new structure and migrate reads/writes to it, then drop the obsolete structure in a later migration once no live code version references it.
- **Apply migrations before dependent code**: run pending schema migrations before starting or routing traffic to code that requires the new schema, and consider a deployment incomplete until all environment migrations finish.
- **Application-embedded databases migrate at startup**: when each installation owns its own local database file that no central deployment pipeline reaches, the application applies pending migrations automatically at startup before running application queries.

## Query Shape and Parameters

- **Execute set operations in the database**: perform filtering, joining, sorting, projection, and aggregation in the query engine rather than loading broad row sets to discard or aggregate in application memory; the only exception is small, stable reference data explicitly cached whole in memory (e.g., lookup tables).
- **Avoid per-row query loops**: fetch all records needed for an operation, screen, or report in bulk queries rather than issuing a separate query inside a loop over prior result rows.
- **Select explicit columns**: name required columns explicitly rather than using wildcard column selection (`SELECT *`), so schema additions cannot break positional reads or transfer unneeded data.
- **Keep search predicates index-friendly**: write filter and join conditions directly against indexed columns rather than wrapping columns in functions or expressions that prevent index usage (sargable predicates).
- **Keep bound parameter counts within engine limits**: whenever bound parameters grow with input size (e.g., list filters or multi-row inserts), keep every query below the database engine's hard parameter ceiling for all possible input sizes — e.g., via structured/table parameters, native bulk load mechanisms, or chunking with a named constant batch size below the limit. When chunking one logical all-or-nothing write across multiple statements, execute all chunks inside a single transaction (`standard.md` Writes That Hold Together) unless the operation qualifies for multi-transaction bulk batching under `Transactions, Concurrency, and Batching` below.
- **Declare explicit parameter types and sizes**: specify target column types and lengths explicitly on bound parameters rather than letting drivers infer them per call, preventing implicit type conversions that disable indexes and preventing per-length query plan bloat.
- **Bound and deterministically order list queries**: enforce an explicit limit or pagination on list-returning queries that can grow with stored data, and append a unique tie-breaker column (e.g., primary key) to sort orders so pages never duplicate or skip rows.
- **Stream large result sets**: process large or unbounded query results incrementally row-by-row rather than materializing entire result sets into memory at once. When the same job changes the rows it reads, read and change them one bounded chunk at a time instead of writing while the stream is open (`standard.md` Snapshot Before Mutate).
- **Set explicit query timeouts**: attach bounded execution timeouts to database commands and connections so stalled queries or lock waits never hang callers indefinitely.

## Transactions, Concurrency, and Batching

- **Keep transactions short and pure-database**: span only the database operations that must succeed or fail atomically (`standard.md` Writes That Hold Together); never pause inside an open transaction for user interaction or outbound network calls.
- **Choose isolation intentionally without dirty reads**: select the isolation level deliberately for the operation's consistency requirements (`specs.md` SHARED), and prohibit dirty reads (e.g., `READ UNCOMMITTED` or `NOLOCK` hints) that expose uncommitted rows or miscount moving data.
- **Batch large bulk writes cleanly**: when a bulk write (e.g., insert, update, or delete) touches enough rows that holding one giant transaction would trigger lock escalation, block concurrent users, or exhaust transaction logs — and no group of writes valid only together (`standard.md` Writes That Hold Together — Atomic units) spans chunks, and concurrent readers do not require all chunks to become visible together as one atomic snapshot (`specs.md` PARTIAL; `standard.md` Writes That Hold Together — Atomic units) — split it into bounded per-chunk transactions rather than holding one lock-heavy transaction across all chunks. Each chunk executes atomically, and the overall bulk operation is designed to be safe to re-run (idempotent) so an interruption midway converges cleanly on retry.
- **Retry transient concurrency failures carefully**: retry deadlocks or serialization conflicts only up to a fixed attempt limit with randomized backoff (`specs.md` LOOPS) and only when the transaction performs no non-rollbackable external side effects (`specs.md` REPEAT).

## Access Control and Text Comparison

- **Least-privilege runtime account**: where the database engine supports separate database roles (e.g., server-based databases), configure the runtime application connection with only the data permissions required for normal operation — never schema-modification or database-administrator rights — and run migrations under a separate privileged credential. Where an embedded single-file database offers no separate runtime role and the application executes startup migrations (`Deploying Schema Changes`), restrict DDL execution strictly to startup migration routines.
- **Encrypted connections verify the server**: a TLS connection to the database validates the server's certificate, and validation is never switched off (e.g., `TrustServerCertificate=True`, a callback that accepts every certificate); a server whose certificate does not validate is given one that does, or the client trusts that one certificate specifically.
- **Align text collation rules with application rules**: configure database column and query collations (case, width, accent, and kana sensitivity) to match the application's comparison semantics (`values.md` Text Length and Equivalence, when present) so filtering, sorting, and uniqueness constraints behave identically on both sides.
