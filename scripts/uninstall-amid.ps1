param(
    [switch]$RemoveRelease,
    [switch]$RemoveUserData
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$releaseRoot = Join-Path $repoRoot "artifacts\release"
$userDataRoot = Join-Path $env:LOCALAPPDATA "AMID"

Write-Host "Close AMID before uninstalling."
Write-Host "Remove the Chrome extension manually from chrome://extensions."

if ($RemoveRelease -and (Test-Path $releaseRoot)) {
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
    Write-Host "Removed release folder: $releaseRoot"
}

if ($RemoveUserData -and (Test-Path $userDataRoot)) {
    Remove-Item -LiteralPath $userDataRoot -Recurse -Force
    Write-Host "Removed AMID user data: $userDataRoot"
}

if (!$RemoveRelease -and !$RemoveUserData) {
    Write-Host ""
    Write-Host "No files were removed. Use:"
    Write-Host "  .\scripts\uninstall-amid.ps1 -RemoveRelease"
    Write-Host "  .\scripts\uninstall-amid.ps1 -RemoveRelease -RemoveUserData"
}
