param(
    [string]$Configuration = "Release",
    [string]$ReleaseRoot = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $repoRoot ".dotnet\dotnet.exe"
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { "dotnet" }

$env:DOTNET_CLI_HOME = $repoRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:NUGET_PACKAGES = Join-Path $repoRoot ".nuget\packages"

if ([string]::IsNullOrWhiteSpace($ReleaseRoot)) {
    $releaseRoot = Join-Path $repoRoot "artifacts\release"
}
else {
    $releaseRoot = [System.IO.Path]::GetFullPath($ReleaseRoot)
}
$appOutput = Join-Path $releaseRoot "AMID"
$extensionOutput = Join-Path $releaseRoot "chrome-extension"
$projectPath = Join-Path $repoRoot "AMID\AMID.csproj"
$setupProjectPath = Join-Path $repoRoot "AMID.Setup\AMID.Setup.csproj"
$setupOutput = Join-Path $releaseRoot "_setup"
$nugetConfigPath = Join-Path $repoRoot "NuGet.Config"
$appExtensionOutput = Join-Path $appOutput "chrome-extension"

function Invoke-Dotnet {
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE`: $($Arguments -join ' ')"
    }
}

New-Item -ItemType Directory -Force $releaseRoot | Out-Null
if (Test-Path $appOutput) {
    Remove-Item -LiteralPath $appOutput -Recurse -Force
}
if (Test-Path $extensionOutput) {
    Remove-Item -LiteralPath $extensionOutput -Recurse -Force
}
if (Test-Path $setupOutput) {
    Remove-Item -LiteralPath $setupOutput -Recurse -Force
}

Invoke-Dotnet restore $projectPath `
    --runtime win-x64 `
    --configfile $nugetConfigPath `
    --packages (Join-Path $repoRoot ".nuget\packages")

Invoke-Dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained false `
    --no-restore `
    --output $appOutput

Invoke-Dotnet restore $setupProjectPath `
    --runtime win-x64 `
    --configfile $nugetConfigPath `
    --packages (Join-Path $repoRoot ".nuget\packages")

Invoke-Dotnet publish $setupProjectPath `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --no-restore `
    "-p:PublishSingleFile=true" `
    "-p:EnableCompressionInSingleFile=true" `
    --output $setupOutput

Copy-Item -LiteralPath (Join-Path $repoRoot "chrome-extension") -Destination $extensionOutput -Recurse
if (Test-Path $appExtensionOutput) {
    Remove-Item -LiteralPath $appExtensionOutput -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $repoRoot "chrome-extension") -Destination $appExtensionOutput -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination (Join-Path $releaseRoot "README.md")
Copy-Item -LiteralPath (Join-Path $setupOutput "AMID.Setup.exe") -Destination (Join-Path $releaseRoot "install.exe")
Copy-Item -LiteralPath (Join-Path $setupOutput "AMID.Setup.exe") -Destination (Join-Path $appOutput "uninstall.exe")
Copy-Item -LiteralPath (Join-Path $repoRoot "scripts\install-portable.ps1") -Destination (Join-Path $releaseRoot "install-portable.ps1")
Copy-Item -LiteralPath (Join-Path $repoRoot "scripts\uninstall-amid.ps1") -Destination (Join-Path $releaseRoot "uninstall-amid.ps1")
Copy-Item -LiteralPath (Join-Path $repoRoot "scripts\apply-update.ps1") -Destination (Join-Path $appOutput "apply-update.ps1")
Remove-Item -LiteralPath $setupOutput -Recurse -Force

Write-Host "Release created:"
Write-Host $releaseRoot
Write-Host ""
Write-Host "Run:"
Write-Host (Join-Path $appOutput "AMID.exe")
