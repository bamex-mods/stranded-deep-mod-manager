# Stranded Deep Mod Manager v0.2.0

Standalone Windows mod manager for the BamEx Stranded Deep mod ecosystem.

## What changed in v0.2.0


Catalog verification resolves the public `main` branch to an immutable Git commit before downloading `stable/catalog.json` and its SHA-256 file. This prevents branch/CDN cache skew between the catalog and checksum.
v0.2.0 removes the development-time dependency on local `mod-work` folders.

The manager now:

- downloads the public stable catalog from GitHub;
- verifies the catalog with its published SHA-256 file;
- falls back to the last verified cached catalog when GitHub is temporarily unavailable;
- downloads versioned GitHub Release ZIPs on demand;
- caches downloaded packages under LocalAppData;
- verifies every downloaded ZIP against the SHA-256 from the catalog;
- verifies `manifest.json` and `files.json` before installation;
- auto-detects common Steam / SteamLibrary locations;
- provides a Browse button when the game cannot be found automatically.

## Current stable catalog

The manager uses:

`https://raw.githubusercontent.com/bamex-mods/stranded-deep-mod-catalog/main/stable/catalog.json`

Only mods present in that public stable catalog are shown.

## Installation safety

Before installing or updating a package, the manager verifies:

1. the public catalog SHA-256;
2. the downloaded release ZIP SHA-256;
3. package id and version;
4. `manifest.json`;
5. every entry in `files.json`, including size and SHA-256;
6. that package runtime payload is covered by explicit `ownedPaths`;
7. that `ownedPaths` stay inside `BepInEx/plugins`.

The manager never removes the whole `BepInEx` directory or the whole `BepInEx/plugins` directory.

## Existing manual installs

If the currently installed files exactly match a public release, the manager can adopt that installation without reinstalling it.

Manager state is stored under:

`<GameRoot>\BepInEx\ModManager\installed\<package-id>.json`

Gameplay mods do not depend on this state.

When an existing manager state was created from an older pre-public package with the same version number, v0.2.0 does not trust the version string alone. It compares the current runtime against the current public release payload. If the runtime is an exact match, the manager refreshes its state metadata without reinstalling the mod. If the same version contains different runtime bytes or extra package-owned files, the UI reports `Different build`.

`Refresh` may repair manager metadata under `BepInEx\ModManager\installed`, but it does not modify files inside package-owned runtime paths under `BepInEx\plugins`.

## Backups and rollback

Backups are stored under the current Windows user's LocalAppData:

`BamEx\StrandedDeepModManager\backups\`

For persistent packages with `backupBeforeUpdate=true`, the manager also backs up the BepInEx config directory and Stranded Deep save Data before destructive update/uninstall operations.

Persistent save/config data is not deleted during normal uninstall.

If an install or uninstall operation fails after runtime files were changed, the manager attempts to restore the package-owned runtime files from the backup.

## Cache

Verified catalogs and release packages are cached under:

`BamEx\StrandedDeepModManager\cache\`

A cached release ZIP is reused only if its SHA-256 still matches the public catalog.

## Requirements

- Windows
- Stranded Deep
- BepInEx installed in the game directory
- Internet access for the first catalog/package download

After a catalog/package has been cached and verified, the cached copy can be used when GitHub is temporarily unavailable.

## Build

From Windows PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Output:

`build\StrandedDeepModManager.exe`
