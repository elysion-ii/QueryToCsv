# Documentation

Shared rules for every non-source document in the repository (e.g., specifications,
guides, references, ADRs, investigations, notes, plans, and routing entry files).
Strictly follow these rules for all document classification, naming, placement,
writing, and retirement.

## Principles

- **Valid documents only in active paths**: active category folders (every folder outside `archive/` and `adr/archive/`) hold only currently valid documents; never mix retired documents with active ones.
- **Classify by role**: organize documents by role (e.g., specification, rule, decision, procedure, reference, investigation, note) rather than by feature area.
- **Flat active folders**: place active documents directly inside their category folder (or directly under `<App>/` when split per application under `Application Scope`) so readers and agents can discover every active document for a role or application by listing a single directory without guessing ad-hoc topic subfolders; never create `current/` or `active/` subfolders (redundant with `Valid documents only in active paths`) except where lifecycle state itself structures the directory (`investigations/`, `adr/archive/`).
- **Archive vs delete**: archiving removes obsolete documents with historical traceability from active paths while keeping version-control history; delete documents that carry no future reference value.

## Document Responsibilities

- `AGENTS.md`: router holding repository facts, commands, the Applications table, the path of the code map (`Code Map`), and reading instructions — no rule text. A sentence pointing to a rule ("pass the AUDIT gate in `standard.md`") is a reading instruction; a sentence stating an obligation itself ("run tests before committing") is rule text.
- `docs/rules/`: development and change rules (shared rules, stack rules, and application rules; mechanical tooling enforces what it can, and these files govern the obligations code and documents must uphold).
- `docs/specs/`: externally observable behavior the system must exhibit.
- `docs/adr/`: adopted architectural decisions, reasoning, consequences, rejected alternatives, and revisit conditions.
- `docs/guides/`: reproducible working procedures.
- `docs/references/`: durable factual knowledge consulted during work.
- `docs/investigations/`, `docs/notes/`, `docs/plans/`, `docs/inbox/`, and `docs/archive/`: single-purpose roles defined in Category Notes below; never blend them.
- **Input/output test vectors belong in tests**: place exhaustive input → output specification expectations in table-driven test code rather than prose specifications; human-readable reference mapping tables maintained under `docs/references/` follow `Source of Truth` below.

## Writing

Applies to all non-source documents in the repository. Every document must stand on its own for a blank-slate reader who knows nothing of past conversations, earlier versions, or unstated assumptions — two readers reading only the document must reach the same understanding and verdict without guessing.

- **Current facts and purpose first**: state the current state, current constraints, and *what*/*why* before *how*, omitting change history, avoided alternatives, conversation residue, and filler (change history belongs in version control).
- **State the property, not a proxy or temporary tool**: state the invariant property or goal to protect before naming any concrete mechanism that satisfies it (specifications omit implementation mechanisms per `Category Notes`), and include numbers only when the number itself is an enforceable criterion (e.g., system limit, protocol threshold, default).
- **Normative requirements (`docs/rules/`, `docs/specs/`, `docs/adr/`, acceptance criteria)**:
  - **Checkable criteria and boundaries**: state observable pass/fail conditions (never vague adjectives alone such as "simple" or "clean"), define boundary terms, mark whether a list is illustrative (`e.g.`) or exhaustive (`only`), and state both sides of any conditional permission or exception.
  - **Include non-obvious reasons**: state why a requirement or prohibition holds inside the requirement itself so a reader facing an edge case preserves its intent rather than guessing or defending wording blindly.
  - **Explicit interaction on overlaps**: whenever two requirements govern the same artifact or boundary, state how they interact instead of leaving the reader to pick one.

### Requirement IDs

- **Every requirement carries an ID**: every specification requirement appears as a heading opening with `<ID>: <statement>` (e.g., `### ORD-1: An order without lines is rejected`). Content under that heading up to the next same- or higher-level heading constitutes that requirement; un-IDed headings only group requirements and state none themselves.
- **ID syntax**: `<PREFIX>-<number>` where `<PREFIX>` begins with an uppercase ASCII letter and contains only uppercase ASCII letters and digits (e.g., `ORD-1`, `CLI2-14`).
- **Unique and non-reusable**: each ID uniquely identifies one requirement across the repository's specifications; retired IDs are never reassigned.
- **Legacy migration**: any older specification lacking requirement IDs assigns IDs to all requirements on the first edit that touches it.

### Procedure Design

Applies to ordered operational procedures written for humans or agents (e.g., `docs/guides/`, release procedures, setup checklists) so an executor unfamiliar with the environment can follow them safely without guessing.

- **Explicit start, context, and confirmation**: state preconditions and goal state upfront, name the exact operational context (e.g., working directory, host, screen, URL) at each step and transition, and give every step — including conditional or failure-recovery branches — concrete actions and an observable confirmation (e.g., "command exits 0") rather than a vague "done".
- **Order by real dependencies**: sequence steps by prerequisites and verification gates rather than patching broken order with ad-hoc workarounds, using stage headings only when a procedure spans distinct stages.
- **Separate automation from human gates**: automate repetitive mechanical tasks in scripts or tooling, and explicitly mark irreversible operations and judgment calls as manual confirmation steps.

## Directory Layout

```text
AGENTS.md                   router: repository facts, commands, Applications table,
                            code map path, reading instructions — no rule text

docs/
  rules/                    rule bodies: shared rules + stack rules (managed by
                            dev-standards) + application rules
  adr/                      active ADRs
    archive/                superseded / rejected / withdrawn ADRs
  specs/                    specifications: externally observable system behavior
  guides/                   reproducible procedures
  references/               durable repository facts
  investigations/
    active/                 ongoing investigations
    closed/                 finished investigations worth keeping
  notes/                    lightweight, classified valid memos
  plans/                    in-progress work plans (excluded from version control unless enabled)
  inbox/                    unclassified documents awaiting triage
  archive/                  retired non-ADR documents, mirrored by category
    rules/ specs/ guides/ references/ investigations/ notes/ plans/
```

Repositories may add dedicated role subfolders (e.g., `docs/design/`) when multiple documents share a distinct role fitting no standard folder; added categories follow identical placement, archiving, and front-matter rules.

## Application Scope

One scope rule applies across all categories: a document directly under `docs/<category>/` is repository-wide, while a document at `docs/<category>/<App>.md` or under `docs/<category>/<App>/` belongs to that application. Single files and directories are interchangeable per category based on file count, using the exact same application identifier across all categories.

- **Exact application identifier**: choose one identifier per application (matching the project or solution name is recommended, not mandatory) and use it verbatim across filenames and directories, exempt from lowercase kebab-case naming.
- **Mandatory rules/specs pair**: each application declares its `docs/rules/<App>...` and `docs/specs/<App>...` paths explicitly in the root `AGENTS.md` Applications table; splitting other categories per application is optional.
- **Independent ADR numbering**: `docs/adr/` and each `docs/adr/<App>/` maintain separate monotonic sequences spanning active and archived ADRs; retired app-scoped ADRs move to `docs/adr/archive/<App>/`.
- **Retiring an application**: move its authored rules file and specification to `docs/archive/rules/` and `docs/archive/specs/`; never archive dev-standards-managed rule files.

## Placement Decision Table

| Question | Destination |
|---|---|
| Development or change rule? | `docs/rules/` (repository/application rules in application rules file; shared and stack rules managed by dev-standards) |
| Repository fact, command, or routing instruction (no rule text)? | Root `AGENTS.md` (or nested `AGENTS.md` for a subtree) |
| Folder of a feature, or home of a mechanism shared across features? | The code map (`Code Map`) |
| Exhaustive input → output specification test vectors? | Table-driven test code |
| Record of an architectural decision and its rationale? | `docs/adr/` |
| Externally observable system behavior (specification)? | `docs/specs/` |
| Reproducible working procedure? | `docs/guides/` (excluding generic rules already covered in `docs/rules/`) |
| Durable reference fact consulted during work? | `docs/references/` |
| Record of a specific technical investigation? | `docs/investigations/` |
| Valid, lightweight classified memo? | `docs/notes/` |
| In-progress work plan? | `docs/plans/` (finished: `docs/archive/plans/` or delete per `Archive vs Delete`) |
| Placement undecided? | `docs/inbox/` |
| Retired non-ADR document worth retaining? | `docs/archive/<category>/` |
| Retired ADR? | `docs/adr/archive/` |
| No future reference value? | Delete |

## Category Notes

- **`AGENTS.md` (router)**: holds descriptions, technology stack, Applications table, real commands, the code map path, and reading/AUDIT instructions — zero rule bodies. Never duplicate rule text across `AGENTS.md` and `docs/rules/`.
- **`docs/rules/`**: holds rule bodies across the three tiers defined in `standard.md` (Application rules > Stack rules > Shared rules). Never duplicate rules across files.
- **`docs/specs/`**: specifies externally observable system behavior (e.g., purpose, scope, users and external systems, required behavior, inputs/outputs, error behavior, invariants, non-functional requirements, out of scope — including whichever sections apply to the system) with zero implementation details (no internal classes, libraries, or algorithms). Every requirement carries an ID verified by tests (`testing.md` Requirement Coverage), and decision completeness follows `specs.md`.
- **`docs/adr/`**: records adopted decisions in Nygard structure (`Context`, `Decision`, `Consequences` required; `Alternatives considered`, `Revisit when` when applicable). Superseded ADRs name their successor in `superseded-by` and move to `docs/adr/archive/`, never `docs/archive/`.
- **`docs/guides/`**: holds repository- or application-specific reproducible procedures. Do not duplicate generic procedures already established in shared rule files or external tool documentation; include only repository-specific steps or repository decisions layered atop a shared standard.
- **`docs/references/`**: holds durable facts hard to recover from source alone (e.g., external protocol quirks, auth flows, data mappings, legacy constraints, domain glossary, architecture overviews).
- **`docs/investigations/`**: separates ongoing work (`active/`) from finished reference value (`closed/`, distinct from archive). Promote durable findings on completion: rules → `docs/rules/`, decisions → `docs/adr/`, procedures → `docs/guides/`, facts → `docs/references/`.
- **`docs/notes/` vs `docs/inbox/`**: `inbox/` holds items awaiting classification; `notes/` holds intentionally lightweight valid documents. Placing a document "for now" signals `inbox/`; promote authoritative answers to `references/`.
- **`docs/plans/`**: working area excluded from version control alongside `docs/archive/plans/` unless an explicit application rule under `docs/rules/` enables tracking plans. Repositories tracking plans move completed plans worth retaining to `docs/archive/plans/` (renaming on collision) before committing, or delete completed plans with no retention value per `Archive vs Delete`.

## Code Map

The code map tells where code lives, so that code holding the same knowledge is found before new code is written (`standard.md` One Home per Piece of Knowledge).

- **Location**: `docs/references/code-map.md`; in a multi-application repository, `docs/references/<App>/code-map.md` for each application (`Application Scope`). `AGENTS.md` names its path and holds no copy of its content.
- **Content**: one line for each feature folder and for each home of a mechanism shared across features (e.g., database connection, logging, configuration loading, test infrastructure), giving its path and the knowledge it holds. It lists folders and homes, not individual types, so adding a type inside an existing folder leaves it unchanged.

## Front Matter

Front matter embeds lifecycle state and authoring date inside each file so readers, full-text searches, and moved or copied files show whether a document is current without inspecting directory trees or querying version-control history (which can obscure original authoring dates across renames or squashes). Every document carries YAML front matter with at least `created` and `status`, with four explicit exemptions:

1. Repository-root entry files (e.g., `README.md`, `CHANGELOG.md`, `CLAUDE.md`) and agent routing files (`AGENTS.md`, both repository-root and nested) read directly by external tools or agent harnesses expecting immediate Markdown headings.
2. In-progress plans under `docs/plans/` (front matter added only when archived).
3. ADRs, which omit `created` (their append-only `NNNN` sequence already shows decision order in the filename, with commit history available for exact dates) and carry only `status` plus `supersedes`/`superseded-by` when applicable.
4. Managed files under `docs/rules/` distributed verbatim from dev-standards (repository-authored application rules files carry standard front matter).

```yaml
---
status: active
created: 2026-07-15
---
```

### `status` Vocabulary

| Document type | Allowed values |
|---|---|
| ADR | `accepted` / `superseded` / `rejected` / `withdrawn` |
| Investigation | `active` / `closed` / `archived` |
| Everything else | `active` / `archived` |

Do not invent unlisted status values — a fixed vocabulary keeps lifecycle state consistent across searches and tooling without synonymous drift (e.g., `deprecated` vs `obsolete`) and distinguishes `superseded`, `rejected`, and `withdrawn` ADRs inside `docs/adr/archive/`. Permitted optional fields (only these, keeping metadata keys uniform across documents and searches rather than fragmenting across synonyms such as `tags` vs `topics`; ADRs carry only `supersedes` and `superseded-by` per Exemption 3 above): `last-reviewed`, `topics`, `supersedes`, `superseded-by`.

## Naming

- **Lowercase kebab-case**: name general documents descriptively in lowercase kebab-case (e.g., `database-migration.md`, `authentication-flow.md`) to avoid spaces that break shell commands and Markdown links, prevent case-collision bugs between Windows/macOS and Linux filesystems, and keep filenames uniform across searches rather than mixing `snake_case`, `camelCase`, and hyphens; never use content-free names (e.g., `memo.md`, `temp.md`, `note1.md`, `misc.md`).
- **Application identifiers exempt**: `<App>.md` files and `<App>/` directories preserve the exact casing declared in `AGENTS.md`.
- **No sequential numbers for general docs**: do not prefix general filenames with sequential numbers (`0001-`), which create merge-conflict numbering collisions and clutter titles — use `created` front matter and version-control history for chronology instead (ADRs are the sole exception).
- **ADR sequential IDs**: format ADRs as `NNNN-description.md` using append-only 4-digit zero-padded numbers (`max(docs/adr/, docs/adr/archive/) + 1` per directory scope); never renumber or backfill gaps.
- **Directory casing**: keep documentation infrastructure folders lowercase (`docs/`, `adr/`, `guides/`) so fixed documentation paths resolve identically across case-sensitive and case-insensitive filesystems and automation; source-code folders follow the stack's naming convention.

## Lifecycle

```text
general:        (inbox →) active category (non-ADR, non-investigation) → docs/archive/<category>/
investigations: (inbox →) active → closed → docs/archive/investigations/
adr:            (inbox →) docs/adr/ → docs/adr/archive/
plans:          docs/plans/ → docs/archive/plans/
```

Move retained documents only along one arrow at a time (`inbox/` is optional when the category is already known; finished investigations worth reading move to `closed/`, never straight to `docs/archive/investigations/`). Retire documents from active paths as soon as they are superseded or describe obsolete system behavior. Documents meeting the deletion criteria in `Archive vs Delete` may be deleted directly from any stage (including `inbox/`, active folders, `plans/`, and archive folders) without passing through an archive folder first.

## Archive vs Delete

Archive documents that are no longer valid when their historical context helps trace past incidents or decision rationale. Delete duplicates, spent temporary memos, factually wrong notes with no historical value, emptied behavior ledgers (`specs.md` Ledgers), or documents wholly superseded by version-control history (e.g., finished plans with no retention value).

## Source of Truth

When a human-readable reference document (e.g., a mapping table in `docs/references/`) coexists with automated tests, declare the source of truth in the document itself. Precedence: table-driven tests > reference document > current code. Behavior ledgers (`specs.md` Ledgers) record implementation facts, not intended behavior, and are not reference documents under this precedence. Snapshot tests detect unintended change only and are never a source of truth.
