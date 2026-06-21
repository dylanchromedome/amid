# AMID Chrome Integration

This unpacked Chrome extension sends newly-created HTTP and HTTPS Chrome downloads to AMID while the Windows app is open.

If AMID is not listening on `http://127.0.0.1:51234`, the extension does not cancel the Chrome download. Chrome then downloads normally.

## Install

1. Build and run AMID.
2. Open Chrome.
3. Go to `chrome://extensions`.
4. Turn on `Developer mode`.
5. Click `Load unpacked`.
6. Select this folder:

   `C:\Users\are\Documents\App for Managing Internet Downloads (AMID)\chrome-extension`

## Use

1. Keep AMID open.
2. Start any normal HTTP or HTTPS download in Chrome.
3. The extension posts the URL to AMID.
4. If AMID accepts it, the extension cancels Chrome's copy and AMID downloads the file.
5. If AMID is closed or unreachable, the extension leaves Chrome's download alone.
