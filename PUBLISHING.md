# Publishing to the Playnite add-on database

This guide covers getting **Custom Importer** listed in Playnite's built-in add-on browser
(**Add-ons → Browse**), so users can install and update it from inside Playnite.

Playnite's add-on system uses **two** manifests, both kept in this repo:

| File | Role |
|------|------|
| `addon-manifests/PlayniteCustomImporter_b7e2f4a9-3c1d-4e8a-9f26-5d0c7a1b8e34.yaml` | The **add-on manifest**. This is the file you submit to the [Playnite add-on database](https://github.com/JosefNemec/PlayniteAddonDatabase). It carries the listing metadata (name, author, tags, links) and a pointer — `InstallerManifestUrl` — to the installer manifest. |
| `installer.yaml` | The **installer manifest**. It lists the downloadable packages (version, required API version, release date, and the `.pext` URL). Playnite reads this to know what to install and when an update is available. |

The add-on's identity is its `AddonId`, which must match the `Id` in `extension.yaml`:
`PlayniteCustomImporter_b7e2f4a9-3c1d-4e8a-9f26-5d0c7a1b8e34`.

## How the pieces connect

```
Playnite add-on database (JosefNemec/PlayniteAddonDatabase)
        │  hosts the add-on manifest (this repo's addon-manifests/*.yaml)
        ▼
InstallerManifestUrl ──►  installer.yaml   (attached to each GitHub Release)
        │
        ▼
   PackageUrl        ──►  PlayniteCustomImporter.pext   (attached to the same Release)
```

`InstallerManifestUrl` points at
`https://github.com/TIGamingTV/PlayniteCustomImporter/releases/latest/download/installer.yaml`.
That URL always resolves to the **latest** release's asset, so once the listing is approved you never
have to touch the add-on database again — cutting a new release is enough.

## One-time: get listed

1. **Cut a release** (see below) so `installer.yaml` and the `.pext` exist as release assets.
2. **Fork** [`JosefNemec/PlayniteAddonDatabase`](https://github.com/JosefNemec/PlayniteAddonDatabase).
3. Copy `addon-manifests/PlayniteCustomImporter_b7e2f4a9-3c1d-4e8a-9f26-5d0c7a1b8e34.yaml` into that
   repo (follow its README for the exact folder — currently the manifests live under the root
   `manifests/` tree).
4. Open a pull request. Playnite's validation checks that the `InstallerManifestUrl` resolves and that
   the installer manifest is well-formed, so make sure step 1 is done first.
5. Once merged, the add-on appears in Playnite's **Browse** tab.

## Every release: cut a version

Publishing is automated by `.github/workflows/build.yml`. To release version `1.1`, for example:

```bash
git tag v1.1
git push origin v1.1
```

On a `v*` tag the workflow:

- stamps `extension.yaml`'s `Version` from the tag,
- stamps `installer.yaml`'s `Version`, `ReleaseDate`, `PackageUrl`, and `Changelog` to match the tag,
- builds, packages `PlayniteCustomImporter.pext`, and
- publishes a GitHub Release with **both** `PlayniteCustomImporter.pext` and `installer.yaml` attached.

Because `InstallerManifestUrl` uses the `releases/latest/download/` path, Playnite picks up the new
version automatically — no further change to the add-on database is required.

> The `installer.yaml` committed in the repo is the source of truth for its shape; CI rewrites its
> `Version`, `ReleaseDate`, `PackageUrl`, and `Changelog` at release time. The auto-stamped changelog
> is a single generic line pointing at the release notes — for a hand-written changelog, edit the
> committed file's `Changelog:` list (and drop the CI changelog stamp) instead.
