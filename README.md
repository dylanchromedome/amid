# AMID

AMID is a Windows desktop download manager for normal HTTP and HTTPS links. It is built with C#/.NET and WPF.

The Chrome integration is intentionally temporary: Chrome downloads are sent to AMID only while the AMID app is open and listening on `127.0.0.1:51234`. If AMID is closed, removed, or unreachable, the Chrome extension leaves Chrome downloads alone.

## Current Features

- Add HTTP or HTTPS URLs manually.
- Download multiple files at once.
- Show filename, URL, progress, downloaded size, total size, speed, ETA, resume support, and status.
- Cancel active downloads.
- Pause/resume only when the server supports HTTP range requests.
- Persist the download list between app restarts.
- Chrome extension handoff through a local-only helper server.
- Crash report dialog with saved reports under `%LocalAppData%\AMID\CrashReports`.

## Project Layout

```text
AMID/                    WPF desktop app
chrome-extension/        Unpacked Chrome extension
scripts/                 Build/run/uninstall helpers
artifacts/release/       Generated release output
```

## Build From Source

From the repo root:

```powershell
.\scripts\build-release.ps1
```

This publishes the app to:

```text
artifacts\release\AMID
```

The script uses `.dotnet\dotnet.exe` when present, otherwise it uses `dotnet` from `PATH`.

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

After installation, start a normal HTTP or HTTPS download in Chrome while AMID is open. AMID accepts the URL, then the extension cancels Chrome's copy. If AMID is closed, Chrome downloads normally.

## Chrome Extension Uninstall

1. Open Chrome.
2. Go to `chrome://extensions`.
3. Find `AMID Chrome Integration`.
4. Click `Remove`.

No Chrome settings are permanently replaced by AMID. Removing or disabling the extension returns Chrome to normal behavior.

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
