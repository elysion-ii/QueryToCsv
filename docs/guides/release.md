---
status: active
created: 2026-07-27
---

# QueryToCsv Release Procedure

Publishing a new QueryToCsv version: the version bump, the release assets, the tag, and
the GitHub Release.

The entry state is `main` holding every change the release contains, with
`Directory.Build.props` still carrying the previous version. The goal state is a pushed
version commit, an annotated tag `v{version}` on `origin`, a GitHub Release carrying both
release assets, and every previous version's tag and release gone — only the current
version stays published.

## Prerequisites

- The repository root — the directory containing `QueryToCsv.slnx` — is the working
  directory for every command below unless a step names another one
- Inno Setup 6 is installed at `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`;
  `build/Installer.ps1` aborts when that path is absent
- `gh auth status` reports an account with push access to `elysion-ii/QueryToCsv`

## Phase 1 — Commit the version

### 1. Start from a clean `main`

Run `git switch main`, then `git pull`.

Confirmation: `git status --porcelain` prints nothing, and `git branch --show-current`
prints `main`.

### 2. Set the new version

In `Directory.Build.props`, set the `<Version>` element to the new version. It is the
only version definition in the repository; the EXE inherits it and
`build/Installer.ps1` reads it.

Confirmation: `Directory.Build.props` contains `<Version>{version}</Version>` and no
other file in the repository defines a version.

### 3. Add the changelog section

In `CHANGELOG.md`, add a `## [{version}] - YYYY-MM-DD` heading directly below
`## [Unreleased]`, with `### Added` / `### Changed` / `### Fixed` entries describing the
release from a user's perspective.

Confirmation: the heading matches `^## \[{version}\]` exactly — `build/Installer.ps1`
gates on that pattern in Phase 2 and fails the installer build when it is absent.

### 4. Commit both files together

Commit `Directory.Build.props` and `CHANGELOG.md` in one commit, either on their own or
together with the change being released.

Confirmation: `git show --stat HEAD` lists both files in the same commit.

## Phase 2 — Build the release assets

### 5. Build the portable EXE

Run `powershell -ExecutionPolicy Bypass -File build/Build.ps1`. It verifies formatting,
runs the test suite, and publishes the self-contained single-file EXE; a failure in
either gate stops it before publishing.

Confirmation: the command exits 0, `build/QueryToCsv/QueryToCsv.exe` exists, and
`(Get-Item build/QueryToCsv/QueryToCsv.exe).VersionInfo.ProductVersion` prints the new
version.

### 6. Build the installer

Run `powershell -ExecutionPolicy Bypass -File build/Installer.ps1`. It reads `<Version>`
from `Directory.Build.props`, verifies the changelog heading, and injects the version
into `build/Setup_QueryToCsv.iss`.

Confirmation: the command exits 0 and
`build/Installer/QueryToCsv-Setup-{version}.exe` exists.

`build/Menu.bat` option 3 runs steps 5 and 6 in sequence and stops at the first failure.

## Phase 3 — Publish

Every step in this phase is visible to users the moment it succeeds, and a published
tag or release can only be withdrawn manually. Run them only after Phase 2 has produced
both assets.

### 7. Push the version commit

Run `git push origin main`.

Confirmation: `git log --oneline -1 origin/main` prints the same commit as
`git log --oneline -1 main`.

### 8. Create and push the tag

Run `git tag -a v{version} -m "v{version}"`, then `git push origin v{version}`.

Confirmation: `git ls-remote --tags origin` lists `refs/tags/v{version}`.

### 9. Create the GitHub Release

Write the release notes into a file — the body is this version's `CHANGELOG.md`
section without its heading — then run:

```powershell
gh release create v{version} --title "v{version}" --notes-file <notes-file> `
  build/QueryToCsv/QueryToCsv.exe `
  build/Installer/QueryToCsv-Setup-{version}.exe
```

Both assets are mandatory: the portable EXE and the installer are the two documented
download paths in `README.md`.

Confirmation: the command prints the release URL.

## Phase 4 — Verify the published release

### 10. Check the release contents

Run `gh release view v{version} --json tagName,isDraft,assets`.

Confirmation: `tagName` is `v{version}`, `isDraft` is `false`, and `assets` lists both
`QueryToCsv.exe` and `QueryToCsv-Setup-{version}.exe`.

### 11. Check the download page

Open `https://github.com/elysion-ii/QueryToCsv/releases`.

Confirmation: the new release is marked `Latest`, and both assets are downloadable from
it.

## Phase 5 — Retire previous releases

Only the current version stays published, so every previous version's tag and GitHub
Release is removed once the new release is verified. Deleting them is irreversible and
their assets cannot be recovered — run this phase only after Phase 4 confirmed the new
release carries both assets. When `v{version}` is the only published version, this phase
ends at step 12.

### 12. Identify what is still published

Run `gh release list` and `git ls-remote --tags origin`.

Confirmation: both list `v{version}`, and every previous version that remains in either
listing is now known. With no previous version in either listing, the release is
complete.

### 13. Delete every previous release and tag

A tag must never outlive its release, so remove both for each previous version:

```powershell
gh release delete v{previous} --yes --cleanup-tag
git tag -d v{previous}
```

Confirmation: `gh release list` shows only `v{version}`, `git ls-remote --tags origin`
lists only `refs/tags/v{version}`, and `git tag -l` no longer prints a previous version.
