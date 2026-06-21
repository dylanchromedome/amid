# AMID

AMID is a Windows desktop download manager for normal HTTP and HTTPS links. It is built with C#/.NET and WPF.

The Chrome integration is intentionally temporary: Chrome downloads are sent to AMID only while the AMID app is open and listening on `127.0.0.1:51234`. If AMID is closed, removed, or unreachable, the Chrome extension leaves Chrome downloads alone.

## Current Features

- Add HTTP or HTTPS URLs manually.
- Download multiple files at once.
- Show filename, URL, progress, downloaded size, total size, smoothed speed/ETA, resume support, and status.
- Cancel active or failed downloads; canceling an unfinished row removes leftover partial files.
- Pause only when the server supports HTTP range requests; use Retry to continue supported partial downloads or start failed ones again.
- Persist the download list between app restarts.
- Keep rows from previous app launches in Old; completed rows still show `Completed` there.
- Chrome extension handoff through a local-only helper server.
- Crash report dialog with saved reports under `%LocalAppData%\AMID\CrashReports`.

## Project Layout

```text
AMID/                    WPF desktop app
chrome-extension/        Unpacked Chrome extension
scripts/                 Build/run/uninstall helpers
artifacts/release/       Generated release output
artifacts/dist/          Generated portable zip packages
```

## Build From Source

From the repo root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
```

This publishes the app to:

```text
artifacts\release\AMID
```

The script uses `.dotnet\dotnet.exe` when present, otherwise it uses `dotnet` from `PATH`.

## Portable Package

Create the portable release zip:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-portable.ps1
```

The zip is created at:

```text
artifacts\dist\AMID-portable-win-x64-v0.6.0.zip
```

Install the portable build without admin rights:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\artifacts\release\install-portable.ps1
```

Default install location:

```text
%LocalAppData%\Programs\AMID
```

Optional desktop shortcut:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\artifacts\release\install-portable.ps1 -DesktopShortcut
```

## Run The Release Build

```powershell
.\scripts\run-release.ps1
```

Or run directly:

```powershell
.\artifacts\release\AMID\AMID.exe
```

If Windows says the .NET Desktop Runtime is missing, install the .NET 8 Desktop Runtime, then run AMID again.

## Chrome Extension Install

1. Build and run AMID.
2. Open Chrome.
3. Go to `chrome://extensions`.
4. Enable `Developer mode`.
5. Click `Load unpacked`.
6. Select the release extension folder:

```text
C:\Users\are\Documents\App for Managing Internet Downloads (AMID)\artifacts\release\chrome-extension
```

During development, you can also load the source folder:

```text
C:\Users\are\Documents\App for Managing Internet Downloads (AMID)\chrome-extension
```

For the portable install, load this folder:

```text
%LocalAppData%\Programs\AMID\chrome-extension
```

After installation, start a normal HTTP or HTTPS download in Chrome while AMID is open. AMID accepts the URL, then the extension cancels Chrome's copy. If AMID is closed, Chrome downloads normally.

## Chrome Extension Uninstall

1. Open Chrome.
2. Go to `chrome://extensions`.
3. Find `AMID Chrome Integration`.
4. Click `Remove`.

No Chrome settings are permanently replaced by AMID. Removing or disabling the extension returns Chrome to normal behavior.

## GitHub Auto Updates

AMID checks the latest public release from:

```text
https://api.github.com/repos/dylanchromedome/amid/releases/latest
```

If the newest release tag is higher than the app version in `AMID\AMID.csproj`, AMID shows an update popup. Pressing `Update` downloads the portable `.zip` asset from that GitHub release, closes AMID, replaces the installed app files, and reopens AMID.

Release tag rules:

- Use tags like `v0.6.1`, `v0.7.0`, or `v1.0.0`.
- The tag version must be higher than `<Version>` in `AMID\AMID.csproj`.
- Upload the portable zip as a release asset. A good asset name is `AMID-portable-win-x64-v0.6.1.zip`.
- Do not commit `artifacts\release` or `artifacts\dist` into git. They are ignored and should live on GitHub Releases.

## Publishing A GitHub Release

First bump the version in `AMID\AMID.csproj`, for example:

```xml
<Version>0.6.1</Version>
```

Build the portable package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-portable.ps1
```

Commit and push the source changes:

```powershell
git add AMID scripts README.md chrome-extension
git commit -m "Add portable installer and GitHub updater"
git push origin main
```

Create and push a matching tag:

```powershell
git tag v0.6.1
git push origin v0.6.1
```

Then create a GitHub release for that tag at:

```text
https://github.com/dylanchromedome/amid/releases/new
```

Upload:

```text
artifacts\dist\AMID-portable-win-x64-v0.6.1.zip
```

From then on, installed copies of AMID older than `v0.6.1` will offer that update.

## App Uninstall

1. Close AMID.
2. Remove the Chrome extension using the steps above.
3. Delete the release folder:

```text
C:\Users\are\Documents\App for Managing Internet Downloads (AMID)\artifacts\release
```

Optional cleanup:

```powershell
.\scripts\uninstall-amid.ps1 -RemoveRelease -RemoveUserData
```

Remove a portable install:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\uninstall-amid.ps1 -RemovePortableInstall
```

`-RemoveUserData` deletes AMID's saved list and crash reports under `%LocalAppData%\AMID`. It does not delete files from your Downloads folder.

## Troubleshooting

- If Chrome downloads normally while AMID is open, check the AMID status bar. It should say `Chrome: listening on 127.0.0.1:51234`.
- If another program is already using port `51234`, AMID logs `Chrome integration unavailable`; Chrome will continue downloading normally.
- If a download cannot be paused, the server probably did not advertise HTTP range support. AMID will show `Range: No` or `Unknown`.
- If AMID crashes, copy the crash report from the popup or find it in `%LocalAppData%\AMID\CrashReports`.

## Developer Commands

Debug build:

```powershell
$env:DOTNET_CLI_HOME=(Get-Location).Path
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
.\.dotnet\dotnet.exe build .\AMID.sln --no-restore
```

Release publish:

```powershell
.\scripts\build-release.ps1
```
