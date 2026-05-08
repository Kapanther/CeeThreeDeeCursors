# CeeThreeDeeCursors

A lightweight Windows desktop overlay app that gives you a configurable on-screen crosshair for games.

Built with .NET 8 + WPF, designed to run from the system tray with global hotkeys.

## Download

- Latest release: https://github.com/Kapanther/CeeThreeDeeCursors/releases/latest
- Installer file name pattern: `CeeThreeDeeCursorsV#.##.msi`

## Features

- Transparent, click-through crosshair overlay
- Multiple crosshair styles with live preview
- Adjustable color, size, gap, thickness, dot radius, opacity, and outline
- Per-monitor targeting
- Global hotkeys:
  - Toggle crosshair visibility
  - Open settings
- Tray app workflow (starts in tray)
- Per-user MSI installer (no admin required in normal cases)

## Current Defaults

- Toggle hotkey: `Shift+F9`
- Settings hotkey: `Shift+F10`
- App starts in tray with crosshair hidden until toggled on

## Build Requirements

- Windows 10/11
- .NET SDK 8.x
- WiX Toolset (used by the installer project)

## Run Locally

```powershell
cd D:\ceethreedee\CeeThreeDeeCursors
dotnet run --project .\CeeThreeDeeCursors\CeeThreeDeeCursors.csproj
```

## Build the App

```powershell
cd D:\ceethreedee\CeeThreeDeeCursors
dotnet build .\CeeThreeDeeCursors\CeeThreeDeeCursors.csproj -c Release
```

## Build the Installer (MSI)

```powershell
cd D:\ceethreedee\CeeThreeDeeCursors
.\build-installer.ps1
```

Output folder:

- `artifacts/installer`

Versioned MSI example:

- `artifacts/installer/CeeThreeDeeCursorsV1.01.msi`

## Bump Version

Use the version bump script:

```powershell
cd D:\ceethreedee\CeeThreeDeeCursors
.\bump-version.ps1 -Part patch   # or minor / major
```

This updates project version fields in `CeeThreeDeeCursors/CeeThreeDeeCursors.csproj`.

## VS Code Tasks

Available tasks:

- `build`
- `publish`
- `watch`
- `build-installer`
- `version-bump-patch`
- `version-bump-minor`
- `version-bump-major`

Run with:

1. `Ctrl+Shift+P`
2. `Tasks: Run Task`
3. Choose a task

## Publish MSI on GitHub Releases

Recommended flow:

1. Bump version:
   - Run `version-bump-patch` (or minor/major)
2. Build installer:
   - Run `build-installer`
3. Commit + push changes
4. Create a tag matching your app version, for example:
   - `v1.1.0`
5. Draft a GitHub Release for that tag
6. Attach the MSI from `artifacts/installer`
7. Publish the release

After publishing, users can download from:

- https://github.com/Kapanther/CeeThreeDeeCursors/releases/latest

## One-Click Automated Release (GitHub Actions)

This repo includes an automation workflow at [.github/workflows/release-msi.yml](.github/workflows/release-msi.yml).

What it does:

1. Builds the app and MSI on `windows-latest`
2. Finds the versioned MSI in `artifacts/installer`
3. Creates/updates a GitHub Release
4. Uploads the MSI as a downloadable release asset

How to run it:

1. Open GitHub repo Actions tab
2. Select workflow: `Build And Release MSI`
3. Click `Run workflow`
4. Optional: set `tag` (example: `v1.1.0`), otherwise it uses the csproj version

Alternative trigger:

- Push a tag that starts with `v` (example: `v1.1.0`) and it runs automatically.

## One-Click Local Release (No Waiting For Actions)

If you want to publish immediately from your machine, use the local release script.

Script:

- [release-local.ps1](release-local.ps1)

What it does:

1. Builds the MSI locally using `build-installer.ps1`
2. Finds the latest `CeeThreeDeeCursorsV*.msi`
3. Creates a GitHub Release for the current app tag (or updates existing)
4. Uploads the MSI asset (replaces existing asset with `--clobber`)

Prerequisites:

- GitHub CLI installed (`gh`)
- Authenticated once: `gh auth login`

Run manually:

```powershell
cd D:\ceethreedee\CeeThreeDeeCursors
.\release-local.ps1 -Configuration Release
```

Dry run (no upload):

```powershell
.\release-local.ps1 -DryRun
```

VS Code task:

- `release-local`

## Project Structure

- `CeeThreeDeeCursors/` - main WPF app
- `Installer/` - WiX installer project
- `build-installer.ps1` - publishes app and builds MSI
- `bump-version.ps1` - version bump utility
- `.vscode/tasks.json` - build and release helper tasks

## Notes

- Overlay visibility in exclusive fullscreen games depends on game/render mode.
- Borderless Windowed is generally recommended for desktop-overlay tools.
