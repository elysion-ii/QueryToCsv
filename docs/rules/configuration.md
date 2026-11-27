# Configuration Standards

**Applies when** the application reads persistent or environment-supplied settings — installation settings an administrator sets for each installation, or preference settings users or operators may change (e.g., configuration files, environment variables, external configuration stores, or values saved from a settings screen). Does not apply to applications with no configurable settings, to per-invocation command-line arguments (`cli.md`, when present), or to runtime domain data (`standard.md` External Input).

Defines stack-agnostic ideals for validating and defaulting the two kinds of settings,
separating environment targets from code, ordering multiple configuration sources, and
keeping working configurations out of version control. Actual secrets belong in a
platform secret store (`security.md`, when present), never in plaintext configuration
files.

## Configuration Values

Every setting is one of two kinds, and its kind decides how a missing or invalid value is handled:

- **Installation settings fail fast at startup**: a setting whose correct value depends on a decision an administrator makes for each installation (e.g., database endpoint, external service address, the output directory chosen for a site) carries no default; validate it once at startup and halt immediately with a message naming the setting before starting work if it is missing, unparsable, or out of range.
- **Preference settings ship a default valid everywhere**: a setting users or operators may adjust but that no installation must decide (e.g., page size, column order, theme, log retention count) carries a well-chosen built-in default in code (a constant, or a runtime rule such as a per-user folder) that is correct at every installation; the application runs on that default until someone changes it.
- **Check preference changes where they are entered**: a value entered through the application's own UI is validated when entered and rejected with a message stating what was wrong (`standard.md` External Input), so a user's choice is never silently replaced.
- **Fall back when a stored preference is unusable**: when a stored preference value read at startup is missing, unparsable, or out of range, apply its default rather than failing startup.
- **Target environment stays outside code**: never hardcode target databases, service URLs, or shared paths in source code (only a target that a runtime rule resolves correctly at every installation, such as a folder under the current user's profile, may have a default; every other target is an installation setting), so the tested binary matches what runs in production.
- **Validate once on read**: validate installation settings and apply preference defaults while loading configuration at startup so downstream code receives valid typed settings without repeating defensive checks.
- **Log every default substitution once**: whenever a stored preference value falls back to its default, write one log entry naming the setting, the reason (missing, unparsable, or out of range), and the applied default so a typo in a configuration source never alters behavior silently.
- **Externalize only field-adjustable settings**: keep values never altered after deployment as named constants (`standard.md` Named Constants); externalizing a setting incurs all obligations in this section.
- **Boundary with program invariants**: `standard.md` (Enforce Invariants in Code) governs values produced by code (where violations are bugs and fail fast), whereas this section governs settings (where installation settings fail fast at startup and unusable stored preferences fall back and log).

## Source Precedence

- **Deterministic source hierarchy**: when multiple configuration sources supply the same setting, apply a consistent descending precedence: per-invocation overrides (e.g., command-line options; see `cli.md` when present) > environment variables > configuration files > built-in defaults.

## Configuration Files in the Repository

Applies to every file holding installation settings that the application reads at run time (e.g., settings files, deployment descriptors); applications configured solely through environment variables or secret stores need not add configuration files. Preference settings the application writes to a per-user location never appear in the repository or the installer, so this section does not apply to them.

- **Track placeholder templates only**: commit only a non-secret placeholder template (e.g., a template file distinct from the active configuration filename), and keep the active configuration file the application reads untracked by version control so local endpoints and environment-specific settings cannot leak through the repository.
- **Keep developer configurations local**: generate local developer configurations from the tracked template locally, never treating any developer's working copy as shared source of truth.
- **Derive shipped configuration during build**: copy or generate the packaged default configuration file from the tracked template inside the gated build pipeline (`standard.md` Build Hygiene and Static Analysis), never from a working-tree active file.
- **Preserve existing configuration during upgrades**: install the default configuration file only when no active configuration file exists at the target destination, so upgrades never overwrite operator settings.
- **Keep the template structurally complete**: include every non-secret installation setting read from configuration files in the tracked template so the template documents the configurable contract; secrets reside in platform secret stores rather than configuration templates (`security.md`, when present).
