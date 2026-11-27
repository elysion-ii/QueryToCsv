# .NET Client Standards

Stack rule file specifying the .NET and Windows desktop packaging and UI means that satisfy
`client.md`, used alongside `dotnet.md`.

**Applies when** the application is a .NET application packaged, distributed to, and run
directly on Windows user or operator machines (`client.md` applies). Does not apply to
server-hosted web/API applications, cross-platform console tools not packaged for Windows
clients, or scripts executed strictly from a developer source tree.

## Enforcement Matrix

Rules marked **Enforced** are checked mechanically by project properties or build
targets; rules marked **AUDIT** require manual verification during AUDIT (including any
rule with partial mechanical checks). Where a repository lacks a listed mechanism, treat
that entire rule as **AUDIT** until added.

| ID | Rule | Mechanism | Check |
|---|---|---|---|
| XAML-VISIBILITY | WPF `x:ClassModifier="internal"` and WinUI 3 `App` exception (`standard.md` Minimal Visibility) | `.editorconfig` `CA1515` + `[SuppressMessage]` on WinUI 3 `App` | AUDIT — `CA1515` enforces non-exempt `public` types; XAML `x:ClassModifier` and constructor visibility stay AUDIT |
| VIRTUALIZATION | Keep UI virtualization active on scrollable WPF/WinUI collections (`client.md` Responsive UI for Large Lists) | — | AUDIT |
| NATIVEDEP | Forbid self-extracting publish properties, drop unneeded native assets, and exclude `.pdb` files (`client.md` Packaging and Distributable Footprint) | — | AUDIT |

---

## XAML-VISIBILITY: XAML Entry Points

Refines `dotnet.md` (VISIBILITY) for WPF and WinUI 3 XAML types where generated partials must match code-behind modifiers (`CS0262`):

- **WPF**: set `x:ClassModifier="internal"` on `App.xaml` and every XAML root declaring `x:Class` (e.g., windows, `UserControl`, `Page`, and code-behind `ResourceDictionary`) to match `internal partial class` in the code-behind; keep a `public` parameterless constructor on any XAML root instantiated directly by XAML `Activator.CreateInstance` (e.g., `StartupUri`).
- **WinUI 3**: keep non-`App` XAML code-behinds (e.g., windows, `UserControl`, `Page`, and code-behind `ResourceDictionary`) `internal partial class`, and keep `App` `public` with `[SuppressMessage]` for `CA1515` because XAML compilation generates `public partial class App` in `XamlTypeInfo.g.cs` without supporting `x:ClassModifier`; never set `NoTypeInfoCodeGen` to bypass this, as XAML type resolution depends on generated type info.

## VIRTUALIZATION: List Virtualization

Implements `client.md` (Responsive UI for Large Lists) in WPF and WinUI 3:

- **Preserve panel virtualization**: keep UI virtualization active on scrollable collections — for controls with a built-in `ScrollViewer` (`DataGrid`, `ListView`, `ListBox`), never disable virtualization (e.g., setting `VirtualizingPanel.IsVirtualizing="False"` in WPF or replacing WinUI 3's default virtualizing `ItemsPanel` with a non-virtualizing `StackPanel`) and never wrap them inside an outer `ScrollViewer` or an unbounded-axis layout container (e.g., a vertical `StackPanel` for a vertically scrolling list); for WinUI 3 `ItemsRepeater` (which has no built-in `ScrollViewer`), host it inside a viewport-bounded `ScrollViewer` or `ScrollView` with a virtualizing layout rather than inside an unbounded-axis container (e.g., a vertical `StackPanel`).

## NATIVEDEP: Native Dependencies and Publish Layout

Implements `client.md` (Packaging and Distributable Footprint) in .NET:

- **Forbid self-extracting publish properties**: never set `IncludeNativeLibrariesForSelfExtract` or `IncludeAllContentForSelfExtract`, which unpack files into shared temporary folders on launch without cleanup; on frameworks such as WinUI 3 where `PublishSingleFile` implicitly enables `IncludeAllContentForSelfExtract`, publish side-by-side without `PublishSingleFile`.
- **Drop unneeded native assets**: remove unreachable or unnecessary transitive native packages (e.g., via `ExcludeAssets="all"` or managed `AppContext` switches) so unused native binaries do not ship; ship unavoidable runtime/UI native binaries alongside the executable.
- **Exclude debug symbols by default**: keep `.pdb` files out of published packages unless the application rules file explicitly requires shipping symbols for field crash diagnostics (`client.md`).
