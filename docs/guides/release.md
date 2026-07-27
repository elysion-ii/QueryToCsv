---
status: active
created: 2026-07-27
---

# Releasing QueryToCsv

How this repository publishes a version. The generic Git and PR procedure is in
`docs/rules/git.md`; this guide adds what is specific to QueryToCsv: which assets a
GitHub release carries and how they are produced.

## Phase 1: Set the version

1. Update `<Version>` in `Directory.Build.props`. This is the only place a version is
   defined — the EXE inherits it through MSBuild and `build/Installer.ps1` injects it
   into the Inno Setup script (`docs/rules/dotnet.md`, VERSION)
2. If `CHANGELOG.md` exists, add a `## [x.y.z]` section for the new version in the same
   commit. `build/Installer.ps1` refuses to build without it

## Phase 2: Produce the assets

3. Build the executable: `powershell -ExecutionPolicy Bypass -File build/Build.ps1`
   — the format gate and the tests must pass before the publish step runs
4. Build the installer: `powershell -ExecutionPolicy Bypass -File build/Installer.ps1`

Both steps are also reachable from `build/Menu.bat` (option 3, Full Build).

## Phase 3: Publish

5. Create the GitHub release with both assets:

```
gh release create v{version} \
  "build/Installer/QueryToCsv-Setup-{version}.exe" \
  "build/QueryToCsv/QueryToCsv.exe" \
  --title "v{version}" --notes "..."
```

The installer is for users who want folder creation and optional PATH registration; the
bare EXE is for users who drop it somewhere themselves. A release carries both.
