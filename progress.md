# Progress

A running log of what's built, what's in flight, and what's next for **Playnite Custom Importer**.

_Last updated: 2026-07-14_

## Status: working, usable

The plugin is functional end to end: install it, configure a storage location, and import a downloaded
game folder from the sidebar.

## Done

- [x] Core plugin scaffolding — `GenericPlugin` with settings and a left-sidebar **+** ("Import Game")
      button.
- [x] 3-step import wizard: pick download folder → pick game `.exe` → choose storage location.
- [x] Downloads-based folder picker (lists only subfolders containing an `.exe`), with a
      "Change folder…" override and optional configured source folder.
- [x] Executable discovery that filters out installers/redistributables and shows relative paths so
      nested launchers are distinguishable.
- [x] Import moves **only the real game folder** into the storage location; recycles the leftover
      download wrapper (Recycle Bin, non-destructive).
- [x] Cross-drive move fallback (copy + delete).
- [x] Library registration: install directory set, *Play* action added, game marked installed.
- [x] Game-name cleaning (strips scene/repacker tags for better metadata matching).
- [x] Duplicate-import protection (re-importing the same `.exe` is refused).
- [x] Opens the game's metadata editor after import (toggleable in settings).
- [x] Settings UI: source folder, storage locations (add/browse/remove) with free-space display,
      "open editor after import" toggle.
- [x] Background import thread with a progress bar.
- [x] CI (`build.yml`): restore + build on Windows, package `.pext`, publish a Release on `v*` tags.
- [x] Reworked user-friendly README with install + usage guide.
- [x] Added `CLAUDE.md` (contributor/AI guidance) and this `progress.md`.

## Ideas / possible next steps

- [ ] Publish a tagged release so users have a `.pext` to download directly.
- [ ] Automated tests for `GameImporter` (name cleaning, move/cleanup, duplicate detection).
- [ ] Batch import (queue multiple downloaded folders in one pass).
- [ ] Smarter game-`.exe` heuristics (e.g. rank by folder depth / file size).
- [ ] Optional per-storage-location default so step 3 can be skipped.
- [ ] Screenshots/GIF of the wizard in the README.

## Notes

- Windows-only build (.NET Framework 4.6.2 + WPF). Cannot be compiled in a Linux CI container.
- Playnite's plugin API has no fully-silent metadata download, hence the "open editor after import"
  approach.
