# CLAUDE.md

Guidance for AI assistants (and new contributors) working in this repository.

## What this project is

**Playnite Custom Importer** is a [Playnite](https://playnite.link/) `GenericPlugin`. It adds a **+**
button to Playnite's left sidebar that opens a 3-step wizard for importing a downloaded game folder:
it moves the real game folder into a chosen storage location, recycles the leftover download wrapper,
registers the game in the library (installed, with a *Play* action pointing at the `.exe`), cleans the
game name of scene/repacker tags, and optionally opens the metadata editor.

## Tech stack & constraints

- **Language:** C# 7.3
- **Framework:** .NET Framework **4.6.2** with **WPF** (MVVM)
- **SDK:** `PlayniteSDK` 6.16.0 (NuGet; see `packages.config`)
- **Builds on Windows only.** The target framework and WPF references cannot be built on Linux/macOS.
  Do not attempt to compile here — verify changes by reading and, where possible, on a Windows machine
  with Visual Studio / MSBuild.

## Project layout

| Path | Purpose |
|------|---------|
| `PlayniteCustomImporterPlugin.cs` | Plugin entry point. Registers the sidebar button, wires up settings, opens the wizard, and opens the game editor after import. |
| `Import/ImportWizardWindow.xaml` (+ `.xaml.cs`) | The 3-step wizard view. |
| `Import/ImportWizardViewModel.cs` | Wizard state machine (`WizardStep`), step navigation, executable discovery, and running the import. |
| `Import/GameImporter.cs` | Core, UI-free logic: folder move (with cross-drive copy+delete fallback), recycling the wrapper, name cleaning, and Playnite library registration. |
| `Settings/PlayniteCustomImporterSettings.cs` | Persisted settings model + `ISettings` view-model (Begin/Cancel/EndEdit lifecycle). |
| `Settings/StorageLocation.cs` | A named `Name → Path` destination, including free-space display. |
| `Settings/PlayniteCustomImporterSettingsView.xaml` (+ `.xaml.cs`) | The settings UI. |
| `extension.yaml` | Playnite extension manifest (Id, Name, Version, Module, Icon). |
| `.github/workflows/build.yml` | CI: restore + build on `windows-latest`, package `.pext`, publish a Release on version tags. |

## Conventions

- **MVVM:** UI logic lives in view-models; keep `.xaml.cs` code-behind minimal. Business logic that
  doesn't need Playnite UI belongs in `GameImporter`, which is deliberately testable/UI-free.
- **Playnite API:** access it via the injected `IPlayniteAPI`. Long-running work (the import) runs off
  the UI thread with a progress bar; keep it that way.
- **Errors:** log with the `ILogger` from `LogManager.GetLogger()` and surface user-facing failures via
  `PlayniteApi.Dialogs`. Don't let exceptions escape into Playnite's host.
- **Comments:** the codebase favors explanatory XML-doc summaries on non-obvious members. Match that
  density and tone; explain *why*, not *what*.
- **Non-destructive by default:** the wrapper cleanup uses the Recycle Bin, not permanent deletion.
  Preserve this guarantee in any change to the move/cleanup path.

## Building & releasing

```powershell
nuget restore PlayniteCustomImporter.sln
msbuild PlayniteCustomImporter.sln /p:Configuration=Release /p:Platform="Any CPU"
```

CI packages `PlayniteCustomImporter.dll` + `extension.yaml` + `icon.png` into a `.pext`. To cut a
release, push a `v*` tag — the workflow stamps `extension.yaml`'s version from the tag and attaches the
`.pext` to a GitHub Release.

## Testing

There is no automated test suite. Use the **Manual test checklist** in `README.md` after any change to
the import flow, name cleaning, or move/cleanup logic. When editing `GameImporter`, pay special
attention to: cross-drive moves, nested vs. top-level `.exe` placement, duplicate-import blocking, and
that only the real game folder (not the whole wrapper) is moved.

## Working agreements

- Keep `README.md` (user-facing) and this file (contributor-facing) in sync with behavior changes.
- Update `progress.md` when you complete meaningful work.
- Don't add dependencies casually — this plugin ships as a single DLL.
