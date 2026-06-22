# AMID

AMID is a Windows desktop download manager for normal HTTP and HTTPS links.

The Chrome integration is temporary and safe by design: Chrome downloads are sent to AMID only while AMID is open and listening on `127.0.0.1:51234`. If AMID is closed, removed, or unreachable, Chrome downloads normally.

## Install

1. Go to the AMID releases page:

```text
https://github.com/dylanchromedome/amid/releases/latest
```

2. Download the newest AMID zip, for example:

```text
AMID-portable-win-x64-v0.9.1.zip
```

3. Right-click the zip and choose `Extract All`.

4. Open the extracted folder and run:

```text
install.exe
```

The installer asks for administrator permission, installs AMID to `C:\Program Files\AMID`, creates a Start Menu shortcut so Windows Search can find AMID, and adds `uninstall.exe` to the install folder. It also shows a Chrome extension helper window with the exact extension folder and buttons to copy the path or open the folder.

If Windows says the .NET Desktop Runtime is missing when AMID starts, install the .NET 8 Desktop Runtime, then run AMID again.

## Run Without Installing

You can still run AMID directly from the extracted zip folder:

```text
AMID\AMID.exe
```

Installing is recommended because it adds Windows Search indexing, uninstall support, and Program Files update handling.

## Chrome Extension

The installer shows these steps after AMID is installed. You can also find the same folder helper inside AMID under `Options` with `Copy Path`, `Open Folder`, and `Chrome Page`.

1. Start AMID.
2. Open Chrome.
3. Go to:

```text
chrome://extensions
```

4. Enable `Developer mode`.
5. Click `Load unpacked`.
6. Select the extension folder inside your AMID folder.

If you installed AMID:

```text
C:\Program Files\AMID\chrome-extension
```

If you only unzipped AMID:

```text
<extracted zip folder>\AMID\chrome-extension
```

After this, normal HTTP or HTTPS downloads started in Chrome are sent to AMID while AMID is open. When AMID is closed, Chrome downloads normally.

## Updating

AMID checks GitHub Releases for updates when it starts. If a newer release is available, AMID shows a popup asking whether to update.

Choosing `Update` downloads the newest zip, closes AMID, replaces the app files, and reopens AMID. If AMID is installed in `Program Files`, Windows may ask for administrator permission during the update.

If you loaded the Chrome extension from the AMID folder listed above, reload it in `chrome://extensions` after updating so Chrome uses the newest extension files.

AMID options such as `Close to tray`, `Show on Chrome download`, and `Start with Windows` are saved here:

```text
%LocalAppData%\AMID\settings.json
```

## Uninstall

1. Close AMID from the tray icon menu with `Exit`.
2. Remove the Chrome extension from `chrome://extensions`.
3. Run:

```text
C:\Program Files\AMID\uninstall.exe
```

You can also uninstall AMID from Windows Settings because the installer registers it with Windows.

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
- Close to the notification area instead of quitting.
- Start with Windows and stay ready for Chrome downloads.
- Show the AMID window when Chrome sends a download.
- Show crash details in a popup if AMID hits an unexpected error.

## Troubleshooting

- If Chrome downloads normally while AMID is open, check the AMID status bar. It should say `Chrome: listening on 127.0.0.1:51234`.
- If another program is already using port `51234`, AMID logs `Chrome integration unavailable`; Chrome will continue downloading normally.
- If a download cannot be paused, the server probably did not advertise HTTP range support. AMID will show `Range: No` or `Unknown`.
- If AMID crashes, copy the crash report from the popup or find it in `%LocalAppData%\AMID\CrashReports`.
