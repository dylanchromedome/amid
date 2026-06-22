# AMID

AMID is a Windows desktop download manager for normal HTTP and HTTPS links.

The Chrome integration is temporary and safe by design: Chrome downloads are sent to AMID only while AMID is open and listening on `127.0.0.1:51234`. If AMID is closed, removed, or unreachable, Chrome downloads normally.

## Install

1. Go to the AMID releases page:

```text
https://github.com/dylanchromedome/amid/releases/latest
```

2. Download the newest portable zip, for example:

```text
AMID-portable-win-x64-v0.6.1.zip
```

3. Right-click the zip and choose `Extract All`.

4. Open the extracted folder and run:

```text
AMID\AMID.exe
```

If Windows says the .NET Desktop Runtime is missing, install the .NET 8 Desktop Runtime, then run AMID again.

## Optional Portable Install

The zip can also install AMID into your Windows user profile without admin rights.

From the extracted zip folder, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-portable.ps1
```

Default install location:

```text
%LocalAppData%\Programs\AMID
```

To add a desktop shortcut:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-portable.ps1 -DesktopShortcut
```

## Chrome Extension

1. Start AMID.
2. Open Chrome.
3. Go to:

```text
chrome://extensions
```

4. Enable `Developer mode`.
5. Click `Load unpacked`.
6. Select the extension folder inside your AMID folder.

If you only unzipped AMID:

```text
<extracted zip folder>\AMID\chrome-extension
```

If you used the portable installer:

```text
%LocalAppData%\Programs\AMID\chrome-extension
```

After this, normal HTTP or HTTPS downloads started in Chrome are sent to AMID while AMID is open. When AMID is closed, Chrome downloads normally.

## Updating

AMID checks GitHub Releases for updates when it starts. If a newer release is available, AMID shows a popup asking whether to update.

Choosing `Update` downloads the newest portable zip, closes AMID, replaces the app files, and reopens AMID.

If you loaded the Chrome extension from the AMID folder listed above, reload it in `chrome://extensions` after updating so Chrome uses the newest extension files.

## Uninstall

1. Close AMID.
2. Remove the Chrome extension from `chrome://extensions`.
3. Delete the AMID folder you extracted or installed.

For the default portable install, delete:

```text
%LocalAppData%\Programs\AMID
```

Saved download history and crash reports are stored under:

```text
%LocalAppData%\AMID
```

Deleting that folder removes AMID's saved list and crash reports. It does not delete files from your Downloads folder.

## Features

- Add HTTP or HTTPS URLs manually.
- Download multiple files at once.
- Show filename, URL, progress, downloaded size, total size, smoothed speed/ETA, range support, and status.
- Cancel active or failed downloads.
- Pause only when the server supports HTTP range requests.
- Retry paused or failed downloads.
- Keep old rows separate after app restarts.
- Show crash details in a popup if AMID hits an unexpected error.

## Troubleshooting

- If Chrome downloads normally while AMID is open, check the AMID status bar. It should say `Chrome: listening on 127.0.0.1:51234`.
- If another program is already using port `51234`, AMID logs `Chrome integration unavailable`; Chrome will continue downloading normally.
- If a download cannot be paused, the server probably did not advertise HTTP range support. AMID will show `Range: No` or `Unknown`.
- If AMID crashes, copy the crash report from the popup or find it in `%LocalAppData%\AMID\CrashReports`.
