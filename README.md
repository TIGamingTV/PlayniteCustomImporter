# Playnite Custom Importer

> A [Playnite](https://playnite.link/) extension that turns a downloaded game folder into a
> ready-to-play library entry in a few clicks.

Downloaded a game as a folder (from an archive, a store, a backup drive…) and want it in your
Playnite library **without** the usual manual dance of moving files, creating the entry, pointing it
at the `.exe`, and cleaning up the leftovers? This extension does all of that from a single sidebar
button.

---

## Table of contents

- [What you get](#what-you-get)
- [Install](#install)
- [First-time setup](#first-time-setup)
- [How to import a game](#how-to-import-a-game)
- [Settings reference](#settings-reference)
- [FAQ & troubleshooting](#faq--troubleshooting)
- [For developers](#for-developers)

---

## What you get

A **+** ("Import Game") button on Playnite's left sidebar that opens a short 3-step wizard. When you
finish it, the extension will have:

- ✅ **Moved** the real game folder into a storage location you choose
- ✅ **Cleaned up** the leftover download junk (sent to the Recycle Bin, so it's recoverable)
- ✅ **Added the game** to your library, marked as installed, with a working *Play* button
- ✅ **Cleaned the game's name** (strips scene/repacker tags so metadata matches better)
- ✅ **Opened the metadata editor** so you can download artwork and details with one click

---

## Install

### Option 1 — Install the `.pext` file (recommended)

This is the easiest way and works for everyone.

1. Go to the [**Releases** page](../../releases) and download the latest
   `PlayniteCustomImporter.pext`.
2. **Double-click** the downloaded file (or drag it onto the Playnite window).
3. Playnite asks you to confirm the install — click **Yes**.
4. **Restart Playnite** when prompted.

That's it. You'll see a **+** button on the left sidebar.

> No release yet? You can also grab the `.pext` from the **Actions** tab: open the most recent
> successful build and download the `PlayniteCustomImporter-pext` artifact.

### Option 2 — Manual copy (fallback)

If the `.pext` won't install for some reason, copy the files in by hand:

1. Open this folder (paste the path into File Explorer's address bar):
   ```
   %AppData%\Playnite\Extensions\PlayniteCustomImporter\
   ```
   Create the `PlayniteCustomImporter` folder if it isn't there.
2. Put these three files inside it:
   - `PlayniteCustomImporter.dll`
   - `extension.yaml`
   - `icon.png`
3. **Restart Playnite.**

---

## First-time setup

Before your first import, add at least one **storage location** — the place your games get moved to.

1. In Playnite, go to **Add-ons → Extensions settings → Custom Importer**.
2. Under **Storage locations**, click **Add**, give it a name (e.g. `Games SSD`), and browse to the
   folder where you keep your installed games.
3. (Optional) Set a **Source folder** — where the wizard looks for new downloads. Leave it empty to
   use your **Downloads** folder.
4. Click **Save**.

You only have to do this once.

---

## How to import a game

Click the **+** button on the left sidebar and follow the three steps:

| Step | What you do |
|------|-------------|
| **1. Pick the download** | The wizard lists folders in your Downloads (or source folder) that contain an `.exe`. Select the game's folder and click **Next**. *(Use "Change folder…" to look elsewhere.)* |
| **2. Pick the game's `.exe`** | It lists the `.exe` files inside that folder — installers and redistributables are filtered out, and each is shown with its path so a nested launcher is easy to spot. Select the real game executable and click **Next**. *(Use "Browse for another .exe…" if it isn't listed.)* |
| **3. Pick where it goes** | Choose one of your storage locations and click **Import Game**. |

**What happens on import:** only the folder that actually contains the `.exe` is *moved* into your
storage location. Any leftover wrapper (junk files, other folders) goes to the **Recycle Bin**. The
game is added to your library, marked installed, and pointed at the `.exe`. Unless you turned it off,
the metadata editor opens so you can click **Download Metadata**.

> 💡 Re-importing the same `.exe` is blocked automatically, so you won't create duplicates.

---

## Settings reference

Found under **Add-ons → Extensions settings → Custom Importer**:

| Setting | What it does |
|---------|--------------|
| **Source folder** | Where the wizard looks for games in step 1. Empty = your Downloads folder. |
| **Open the game's editor after import** | Opens the new game's editor so you can download metadata. On by default. |
| **Storage locations** | The named `Name → Path` destinations offered in step 3. Add, browse, and remove them here. |

---

## FAQ & troubleshooting

**The + button doesn't appear.**
Make sure you restarted Playnite after installing, and that the three files (`.dll`, `extension.yaml`,
`icon.png`) are present in the extension folder.

**My download folder isn't listed in step 1.**
The wizard only lists folders that contain an `.exe` somewhere inside. If yours doesn't, use
**Change folder…** to browse to it directly.

**Why do I have to click "Download Metadata" myself?**
The Playnite plugin API doesn't offer a fully-automatic metadata download, so the extension opens the
editor and puts that button one click away.

**Where did my leftover download files go?**
To the **Recycle Bin** — nothing is permanently deleted, so you can restore anything you still need.
(If the `.exe` was already at the top level of the download folder, the whole folder is simply moved
and nothing is deleted.)

---

## For developers

The plugin targets **.NET Framework 4.6.2** and **WPF**, so it must be built on **Windows**.

### Build in Visual Studio
1. Open `PlayniteCustomImporter.sln`.
2. NuGet packages (`PlayniteSDK`) restore automatically on first build.
3. Build in **Release**.

### Build from the command line
```powershell
nuget restore PlayniteCustomImporter.sln
msbuild PlayniteCustomImporter.sln /p:Configuration=Release /p:Platform="Any CPU"
```

### Continuous integration
`.github/workflows/build.yml` restores and builds on `windows-latest`, packages an installable
`PlayniteCustomImporter.pext`, and uploads it as an artifact. Pushing a version tag (e.g. `v1.0`)
also publishes a GitHub Release with the `.pext` attached.

### Project layout
| Path | Purpose |
|------|---------|
| `PlayniteCustomImporterPlugin.cs` | Plugin entry point; registers the sidebar button and opens the wizard. |
| `Import/ImportWizardWindow.xaml` / `.cs` | The 3-step wizard UI. |
| `Import/ImportWizardViewModel.cs` | Wizard state and step navigation. |
| `Import/GameImporter.cs` | Core logic: folder move, cleanup, name cleaning, library registration. |
| `Settings/` | Settings model, view, and the `StorageLocation` type. |
| `extension.yaml` | Playnite extension manifest. |

### Manual test checklist
1. Configure at least one storage location (a source folder is optional; the wizard defaults to
   Downloads).
2. Prepare a test download that mimics a real one: a wrapper folder under Downloads containing junk
   files plus a **subfolder** that holds the game `.exe`.
3. Click **Import Game**. Confirm step 1 lists only folders under Downloads that contain an `.exe`;
   select the wrapper and click **Next**.
4. In step 2, confirm the nested game `.exe` is listed (with its relative path); select it and click
   **Next**.
5. Choose a storage location and click **Import Game**. Confirm that **only** the game subfolder was
   moved, and the original wrapper (with its junk) is now in the **Recycle Bin**.
6. Confirm the new game appears with a cleaned-up name, its install directory is the moved folder, and
   it launches. Re-running the import on the same `.exe` should be refused.
7. Confirm the game's editor opens after import (unless disabled), letting you **Download Metadata**.

---

## License

See [LICENSE](LICENSE).
