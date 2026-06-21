param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "AMID\AMID.csproj"
$releaseRoot = Join-Path $repoRoot "artifacts\release"
$distRoot = Join-Path $repoRoot "artifacts\dist"
$buildScript = Join-Path $repoRoot "scripts\build-release.ps1"

[xml]$project = Get-Content -LiteralPath $projectPath
$version = $project.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read Version from $projectPath."
}

& powershell -NoProfile -ExecutionPolicy Bypass -File $buildScript -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Release build failed."
}

New-Item -ItemType Directory -Force $distRoot | Out-Null
$zipPath = Join-Path $distRoot "AMID-portable-win-x64-v$version.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $releaseRoot "*") -DestinationPath $zipPath -Force

Write-Host "Portable package created:"
Write-Host $zipPath
