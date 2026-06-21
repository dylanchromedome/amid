param(
    [Parameter(Mandatory = $true)]
    [string]$ZipPath,

    [Parameter(Mandatory = $true)]
    [string]$InstallDir,

    [int]$ProcessId = 0,

    [string]$ExePath = ""
)

$ErrorActionPreference = "Stop"

function Resolve-SafeInstallDir {
    param([string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootPath = [System.IO.Path]::GetPathRoot($fullPath)
    if ([string]::IsNullOrWhiteSpace($fullPath) -or $fullPath -eq $rootPath) {
        throw "InstallDir is not safe to update: $fullPath"
    }

    $exe = Join-Path $fullPath "AMID.exe"
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "InstallDir does not contain AMID.exe: $fullPath"
    }

    return $fullPath.TrimEnd('\')
}

function Find-ExtractedAppDir {
    param([string]$ExtractRoot)

    $directApp = Join-Path $ExtractRoot "AMID.exe"
    if (Test-Path -LiteralPath $directApp) {
        return $ExtractRoot
    }

    $nestedApp = Join-Path $ExtractRoot "AMID\AMID.exe"
    if (Test-Path -LiteralPath $nestedApp) {
        return (Join-Path $ExtractRoot "AMID")
    }

    $found = Get-ChildItem -LiteralPath $ExtractRoot -Recurse -Filter "AMID.exe" -File |
        Select-Object -First 1
    if ($null -ne $found) {
        return $found.DirectoryName
    }

    throw "The update package does not contain AMID.exe."
}

if (-not (Test-Path -LiteralPath $ZipPath)) {
    throw "Update package was not found: $ZipPath"
}

$installPath = Resolve-SafeInstallDir $InstallDir
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("AMID\UpdateExtract-" + [guid]::NewGuid())

try {
    if ($ProcessId -gt 0) {
        Wait-Process -Id $ProcessId -Timeout 60 -ErrorAction SilentlyContinue
    }

    Start-Sleep -Milliseconds 750
    New-Item -ItemType Directory -Force $tempRoot | Out-Null
    Expand-Archive -LiteralPath $ZipPath -DestinationPath $tempRoot -Force

    $sourceAppDir = Find-ExtractedAppDir $tempRoot
    Get-ChildItem -LiteralPath $installPath -Force | Remove-Item -Recurse -Force
    Get-ChildItem -LiteralPath $sourceAppDir -Force | Copy-Item -Destination $installPath -Recurse -Force

    $startPath = if ([string]::IsNullOrWhiteSpace($ExePath)) {
        Join-Path $installPath "AMID.exe"
    }
    else {
        $ExePath
    }

    Start-Process -FilePath $startPath
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    Remove-Item -LiteralPath $ZipPath -Force -ErrorAction SilentlyContinue
}
