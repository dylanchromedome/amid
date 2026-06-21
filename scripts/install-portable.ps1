param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA "Programs\AMID"),

    [switch]$DesktopShortcut,

    [switch]$NoStartMenuShortcut
)

$ErrorActionPreference = "Stop"

function Find-SourceAppDir {
    $scriptRoot = $PSScriptRoot

    $releaseApp = Join-Path $scriptRoot "AMID\AMID.exe"
    if (Test-Path -LiteralPath $releaseApp) {
        return (Join-Path $scriptRoot "AMID")
    }

    $appBesideScript = Join-Path $scriptRoot "AMID.exe"
    if (Test-Path -LiteralPath $appBesideScript) {
        return $scriptRoot
    }

    throw "Could not find AMID.exe beside this installer. Extract the portable zip before running this script."
}

function New-Shortcut {
    param(
        [string]$ShortcutPath,
        [string]$TargetPath,
        [string]$WorkingDirectory
    )

    $folder = Split-Path -Parent $ShortcutPath
    New-Item -ItemType Directory -Force $folder | Out-Null

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.Description = "AMID download manager"
    $shortcut.Save()
}

$sourceAppDir = Find-SourceAppDir
$installPath = [System.IO.Path]::GetFullPath($InstallDir).TrimEnd('\')
$installRoot = Split-Path -Parent $installPath
$targetExe = Join-Path $installPath "AMID.exe"

$runningInstall = Get-Process -Name "AMID" -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Path -and $_.Path.StartsWith($installPath, [System.StringComparison]::OrdinalIgnoreCase)
    }

if ($null -ne $runningInstall) {
    throw "Close AMID before installing over $installPath."
}

New-Item -ItemType Directory -Force $installPath | Out-Null
Get-ChildItem -LiteralPath $installPath -Force | Remove-Item -Recurse -Force
Get-ChildItem -LiteralPath $sourceAppDir -Force | Copy-Item -Destination $installPath -Recurse -Force

if (-not $NoStartMenuShortcut) {
    $startMenuShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\AMID.lnk"
    New-Shortcut -ShortcutPath $startMenuShortcut -TargetPath $targetExe -WorkingDirectory $installPath
}

if ($DesktopShortcut) {
    $desktopShortcut = Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "AMID.lnk"
    New-Shortcut -ShortcutPath $desktopShortcut -TargetPath $targetExe -WorkingDirectory $installPath
}

Write-Host "AMID installed:"
Write-Host $installPath
Write-Host ""
Write-Host "Run:"
Write-Host $targetExe
Write-Host ""
Write-Host "Chrome extension folder:"
Write-Host (Join-Path $installPath "chrome-extension")
