param(
    [switch]$RemoveRelease,
    [switch]$RemoveUserData,
    [switch]$RemovePortableInstall,
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA "Programs\AMID")
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$releaseRoot = Join-Path $repoRoot "artifacts\release"
$userDataRoot = Join-Path $env:LOCALAPPDATA "AMID"
$installPath = [System.IO.Path]::GetFullPath($InstallDir).TrimEnd('\')
$startMenuShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\AMID.lnk"
$desktopShortcut = Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "AMID.lnk"

Write-Host "Close AMID before uninstalling."
Write-Host "Remove the Chrome extension manually from chrome://extensions."

if ($RemoveRelease -and (Test-Path $releaseRoot)) {
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
    Write-Host "Removed release folder: $releaseRoot"
}

if ($RemovePortableInstall -and (Test-Path $installPath)) {
    Remove-Item -LiteralPath $installPath -Recurse -Force
    Write-Host "Removed portable install: $installPath"
}

if ($RemovePortableInstall -and (Test-Path $startMenuShortcut)) {
    Remove-Item -LiteralPath $startMenuShortcut -Force
    Write-Host "Removed Start Menu shortcut: $startMenuShortcut"
}

if ($RemovePortableInstall -and (Test-Path $desktopShortcut)) {
    Remove-Item -LiteralPath $desktopShortcut -Force
    Write-Host "Removed Desktop shortcut: $desktopShortcut"
}

if ($RemoveUserData -and (Test-Path $userDataRoot)) {
    Remove-Item -LiteralPath $userDataRoot -Recurse -Force
    Write-Host "Removed AMID user data: $userDataRoot"
}

if (!$RemoveRelease -and !$RemoveUserData -and !$RemovePortableInstall) {
    Write-Host ""
    Write-Host "No files were removed. Use:"
    Write-Host "  .\scripts\uninstall-amid.ps1 -RemoveRelease"
    Write-Host "  .\scripts\uninstall-amid.ps1 -RemovePortableInstall"
    Write-Host "  .\scripts\uninstall-amid.ps1 -RemoveRelease -RemoveUserData"
}
