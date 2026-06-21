$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $repoRoot "artifacts\release\AMID\AMID.exe"

if (!(Test-Path $exePath)) {
    throw "Release build not found. Run .\scripts\build-release.ps1 first."
}

Start-Process -FilePath $exePath
