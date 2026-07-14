# PlayniteCustomImporter

A [Playnite](https://playnite.link/) extension that streamlines importing downloaded game
folders: move a staging folder into one of your library locations and register the game in a
few clicks from a left-sidebar button.

## What it does

Adds an **Import Game** button (a **+** icon) to Playnite's left sidebar. Clicking it opens a
3-step wizard:

1. **Select the game folder** — the wizard lists the subfolders of your **Downloads** folder that
   contain an `.exe` (so you only see plausible games). Use *Change folder...* to look somewhere
   else — or point at the configured source folder, if you set one in the settings.
2. **Choose a storage location** — pick one of your configured storage locations. The selected
   folder is then **moved** into it (with a copy-and-delete fallback for moves across drives).
3. **Pick the executable** — the wizard lists the `.exe` files in the moved folder (top level
   first, falling back to a filtered recursive search that skips installers/redistributables).
   Select one, or use *Browse for another .exe...* to pick a launcher manually. **Add to Playnite**
   registers it as a game — install directory set to the exe's folder, a *Play* action pointing at
   the exe, and the game marked as installed (the same result as adding a game manually by
   executable). The game name is cleaned of scene/repacker tags so Playnite's **Download Metadata**
   search matches better, and re-importing the same executable is blocked to avoid duplicates.

## Settings

Open the plugin settings (Playnite → Add-ons → Extensions settings → Custom Importer):

- **Source folder** — where the wizard looks for games in step 1. Leave it empty to default to
  your **Downloads** folder.
- **Storage locations** — named `Name → Path` destinations offered in step 2. Add, browse for,
  and remove them here.

## Building

The plugin targets **.NET Framework 4.6.2** and **WPF**, so it must be built on Windows.

### Visual Studio
1. Open `PlayniteCustomImporter.sln`.
2. Restore NuGet packages (`PlayniteSDK`) — this happens automatically on first build.
3. Build in **Release**.

### Command line
```powershell
nuget restore PlayniteCustomImporter.sln
msbuild PlayniteCustomImporter.sln /p:Configuration=Release /p:Platform="Any CPU"
```

CI (`.github/workflows/build.yml`) performs the same restore + build on `windows-latest`,
packages an installable `PlayniteCustomImporter.pext`, and uploads it as an artifact. Pushing a
version tag (e.g. `v1.0`) additionally publishes a GitHub Release with the `.pext` attached.

## Installing into Playnite

### From the packaged `.pext` (recommended)

Download `PlayniteCustomImporter.pext` from the latest [Release](../../releases) (or from a build's
Actions artifact), then open it in Playnite (double-click, or drag it onto the Playnite window) and
confirm the install prompt. Restart Playnite when asked.

### Manual copy (fallback)

Copy the build output into a new folder under Playnite's extensions directory:

```
%AppData%\Playnite\Extensions\PlayniteCustomImporter\
```

The folder must contain:

- `PlayniteCustomImporter.dll`
- `extension.yaml`
- `icon.png`

Restart Playnite. The **Import Game** button appears on the left sidebar. Configure the source
and storage folders in the plugin settings before your first import.

## Manual test checklist

1. Configure at least one storage location in settings (a source folder is optional; the wizard
   defaults to Downloads).
2. Click **Import Game** (the **+** on the sidebar). Confirm step 1 lists only the folders under
   Downloads that contain an `.exe`; select one (or use *Change folder...*) and click **Next**.
3. Choose a storage location and click **Next** — confirm the folder moved into that location.
4. Select the `.exe` (or browse for one) and click **Add to Playnite**.
5. Confirm the new game appears in your library with a cleaned-up name, its install directory is
   the moved folder, and it launches. Re-running the import on the same exe should be refused.
