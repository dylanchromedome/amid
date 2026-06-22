param(
    [Parameter(Mandatory = $true)]
    [string]$ZipPath,

    [Parameter(Mandatory = $true)]
    [string]$InstallDir,

    [int]$ProcessId = 0,

    [string]$ExePath = ""
)

$ErrorActionPreference = "Stop"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Quote-Argument {
    param([string]$Value)

    return '"' + $Value.Replace('"', '\"') + '"'
}

function Test-NeedsElevation {
    param([string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $programFiles = [Environment]::GetFolderPath("ProgramFiles")
    $programFilesX86 = [Environment]::GetFolderPath("ProgramFilesX86")

    return $fullPath.StartsWith($programFiles, [System.StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith($programFilesX86, [System.StringComparison]::OrdinalIgnoreCase)
}

function Restart-ElevatedIfNeeded {
    param([string]$ResolvedInstallPath)

    if (-not (Test-NeedsElevation $ResolvedInstallPath) -or (Test-IsAdministrator)) {
        return
    }

    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", (Quote-Argument $PSCommandPath),
        "-ZipPath", (Quote-Argument $ZipPath),
        "-InstallDir", (Quote-Argument $ResolvedInstallPath),
        "-ProcessId", $ProcessId,
        "-ExePath", (Quote-Argument $ExePath)
    ) -join " "

    Start-Process -FilePath "powershell.exe" -ArgumentList $arguments -Verb RunAs
    exit 0
}

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

function Update-UninstallMetadata {
    param([string]$ResolvedInstallPath)

    $appExe = Join-Path $ResolvedInstallPath "AMID.exe"
    $uninstallExe = Join-Path $ResolvedInstallPath "uninstall.exe"
    if (-not (Test-Path -LiteralPath $appExe)) {
        return
    }

    $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($appExe).ProductVersion
    $uninstallKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AMID"
    if (Test-Path $uninstallKey) {
        Set-ItemProperty -LiteralPath $uninstallKey -Name "DisplayVersion" -Value $version
        Set-ItemProperty -LiteralPath $uninstallKey -Name "InstallLocation" -Value $ResolvedInstallPath
        Set-ItemProperty -LiteralPath $uninstallKey -Name "DisplayIcon" -Value "$appExe,0"
        Set-ItemProperty -LiteralPath $uninstallKey -Name "UninstallString" -Value "`"$uninstallExe`""
        Set-ItemProperty -LiteralPath $uninstallKey -Name "QuietUninstallString" -Value "`"$uninstallExe`" --quiet"
    }

    $appPathKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\App Paths\AMID.exe"
    if (Test-Path $appPathKey) {
        Set-Item -LiteralPath $appPathKey -Value $appExe
        New-ItemProperty -LiteralPath $appPathKey -Name "Path" -Value $ResolvedInstallPath -PropertyType String -Force | Out-Null
    }
}

if (-not (Test-Path -LiteralPath $ZipPath)) {
    throw "Update package was not found: $ZipPath"
}

$installPath = Resolve-SafeInstallDir $InstallDir
Restart-ElevatedIfNeeded $installPath
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
    Update-UninstallMetadata $installPath

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
