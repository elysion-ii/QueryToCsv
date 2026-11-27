# Client Distribution and Desktop Standards

**Applies when** the application is packaged, distributed to, and run directly on user or operator machines (e.g., desktop GUI applications, installed local utilities, packaged workstation tools). Does not apply to server-hosted web/API applications, remote container services, or scripts executed strictly from a developer source tree.

Defines stack-agnostic ideals for minimizing and cleaning packaged footprints, handling
running instances during update or removal, bounding local logs, and keeping scrollable UI
lists responsive. Rules for anchoring and keeping application-owned paths deterministic
live in `standard.md` (Application-Owned Paths).

## Packaging and Distributable Footprint

- **Avoid unneeded dependencies, runtime self-extraction, and debug symbols**: remove unneeded dependencies and exclude debug symbols from shipped packages unless the repository's application rules explicitly require shipping symbols for field crash diagnostics; never use self-extracting bundles that unpack runtime or native files into shared temporary folders on launch without automatic cleanup, shipping unavoidable native libraries alongside the executable instead.
- **Include every runtime dependency from the verified build**: assemble packages directly from the verified build's complete runtime output rather than a manually maintained executable-only file list, so side-by-side runtime dependencies are never silently dropped.
- **Derive package version from the single source**: derive the installer or package version solely from the project's single version definition (`standard.md` Version Display), failing packaging fast if missing rather than duplicating version numbers in packaging definitions.

## Installation, Update, and Removal

- **Detect running processes on update and uninstall**: detect open processes locking installed application executables before overwriting files during updates or deleting files during uninstalls, preventing partial removals or locked-file failures.
- **Offer graceful close without forced termination**: in interactive runs, inform the user which application is running and offer graceful shutdown and retry, manual retry, or cancel — never force-kill running processes without explicit user direction, because forced termination discards unsaved work.
- **Close gracefully during unattended runs**: in silent/unattended updates or uninstalls where no UI can be shown, request graceful process closure and proceed once the processes have exited; when they do not exit, end the update or uninstall with a failure exit code rather than force-killing them or hanging silently, because a failed deployment can be rerun while work lost to forced termination cannot be recovered.
- **Never block uninstall on detector failure**: if process detection itself fails or cannot answer during uninstall, let removal proceed rather than preventing users from uninstalling.
- **Preserve operator configuration across updates**: update application binaries without overwriting existing operator configuration files (`configuration.md`, when present) or user data.

## Logs on the Host Machine

- **Bounded rotation and automatic retention**: when the application writes local log files (to a writable location defined by the specification), rotate them on a bounded cycle or size threshold and automatically prune old logs to a bounded retention limit defined by the specification or application rules so logs cannot grow without bound on unattended machines.
- **Delete only application-owned log files**: match strictly this application's own log filename pattern or owned log subdirectory during cleanup; never sweep shared directories.
- **Best-effort log cleanup**: log cleanup failures and continue; never abort application startup or primary operations because an old log could not be deleted.

## Leave No Debris on the Host Machine

- **Bound and clean transient local state**: ensure caches, extracted assets, temporary working files, and other transient artifacts the application writes (never user-saved documents or the application's persistent data) obey bounded retention and crash-resilient cleanup touching solely files owned by this application.

## Responsive UI for Large Lists

- **Keep UI virtualization enabled on scrollable lists**: render scrollable collections (e.g., tables, lists, grids) using viewport-only item virtualization rather than materializing visual controls for every row upfront, and never place virtualized lists inside layout containers that grant unbounded height and silently disable virtualization.
