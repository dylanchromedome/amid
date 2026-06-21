\# Project instructions for Codex



We are building a real Windows desktop download manager.



Follow these rules for every task:



\* Use C# and .NET.

\* Use WPF unless there is a strong reason not to.

\* Make the code compile after every stage.

\* Do not skip files.

\* Do not invent fake APIs.

\* Do not write the whole project in one response unless I explicitly ask.

\* Prefer simple working code over fancy architecture.

\* Give exact commands when setup or running is needed.

\* When changing code, modify the actual project files.

\* After changes, run the build/test command if possible.

\* If something fails, fix it instead of only explaining it.

\* Do not ask me for approval before normal coding research.



Internet and tool rule:

When web access, internet lookup, package download, documentation lookup, or error lookup is available, use it without asking me first.

Do not pause to ask whether you may search the web, check docs, verify APIs, or install normal development dependencies.

Only ask before actions that spend money, publish or send something, delete user files, expose private data, change real accounts, or do something destructive.



App goal:

Build a Windows download manager with a qBittorrent-like layout, but only for normal HTTP and HTTPS download links.



Core app requirements:



\* Default download folder is the current Windows user's Downloads folder.

\* User can add a download URL manually.

\* App shows a download list/table.

\* Show filename, URL, progress percent, downloaded size, total size, speed, ETA, status.

\* Support multiple downloads.

\* Support cancel.

\* Support pause and resume only when realistically possible with HTTP range requests.

\* Failed downloads should show a readable error.

\* Completed downloads should remain in the list.

\* UI should be clean and practical, similar-ish to qBittorrent, but not a clone.



Chrome integration goal:

Chrome downloads should only be redirected into this app while the app is open.

When the app is closed, Chrome should download normally.

Use a Chrome extension plus either native messaging or a localhost helper server.

Do not hack Chrome directly.

Do not replace Chrome settings permanently.

Do not break normal Chrome downloading.



Build stages:

Stage 1: Minimal WPF app skeleton with qBittorrent-like UI. No real downloading yet.

Stage 2: Real download engine.

Stage 3: Pause, resume, cancel, speed, ETA, and persistence.

Stage 4: Chrome extension and integration.

Stage 5: UI optimization and visual polish. Make the UI more accurate to qBittorrent-style desktop download manager design while keeping this app original.

Stage 6: Installer or easy release build.



Important:

Work one stage at a time.

Before coding a stage, briefly say what files will change.

After coding, say how to run it.

Never break working features from earlier stages unless required, and explain why first.

