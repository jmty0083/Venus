#Requires -Version 5.1
<#
.SYNOPSIS
    Adds the LogViewer build output directory to the current user's PATH so that
    'view' and 'LogViewer' can be called from any cmd or PowerShell session.

.DESCRIPTION
    Searches for the built executables under common output locations relative to
    this script's directory, then appends that directory to the User PATH in the
    Windows registry. The change takes effect in any new shell session.

.EXAMPLE
    # From the repo root after building:
    .\install.ps1

.EXAMPLE
    # Point directly to a specific directory:
    .\install.ps1 -BinDir ".\LogViewer\bin\Release\net10.0-windows"
#>
param(
    [string] $BinDir = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Locate the binary directory ───────────────────────────────────────────────

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

if ($BinDir -ne "") {
    $resolved = Resolve-Path $BinDir -ErrorAction SilentlyContinue
    if (-not $resolved) {
        Write-Error "Specified BinDir not found: $BinDir"
        exit 1
    }
    $targetDir = $resolved.Path
} elseif (Test-Path (Join-Path $scriptDir "view.exe")) {
    # Script is sitting next to the exe (distributed/published layout) — use its own directory
    $targetDir = $scriptDir
} else {
    # Script is at the repo root — search common build output locations
    $candidates = @(
        "LogViewer\bin\Debug\net10.0-windows",
        "LogViewer\bin\Release\net10.0-windows",
        "LogViewer\bin\Debug\net9.0-windows",
        "LogViewer\bin\Release\net9.0-windows"
    )

    $targetDir = $null
    foreach ($rel in $candidates) {
        $path = Join-Path $scriptDir $rel
        if (Test-Path (Join-Path $path "view.exe")) {
            $targetDir = $path
            break
        }
    }

    if (-not $targetDir) {
        Write-Error @"
Could not find view.exe in any of the expected output directories.
Please build the project first (dotnet build or Visual Studio Build),
then re-run this script.

You can also specify the directory manually:
  .\install.ps1 -BinDir '<path-to-bin-folder>'
"@
        exit 1
    }
}

$targetDir = [System.IO.Path]::GetFullPath($targetDir)

# ── Check if already in PATH ──────────────────────────────────────────────────

$rawPath  = [System.Environment]::GetEnvironmentVariable("PATH", "User")
$userPath = if ($rawPath) { $rawPath } else { "" }
$entries  = $userPath -split ";" | Where-Object { $_ -ne "" }

if ($entries -contains $targetDir) {
    Write-Host "Already in PATH: $targetDir" -ForegroundColor Cyan
    Write-Host "No changes made."
    exit 0
}

# ── Add to User PATH ──────────────────────────────────────────────────────────

$newPath = ($entries + $targetDir) -join ";"
[System.Environment]::SetEnvironmentVariable("PATH", $newPath, "User")

Write-Host ""
Write-Host "Added to User PATH:" -ForegroundColor Green
Write-Host "  $targetDir" -ForegroundColor White
Write-Host ""
Write-Host "Open a new terminal and try:" -ForegroundColor Yellow
Write-Host "  view app.log"
Write-Host "  LogViewer app.log"
Write-Host "  cat app.log | view"
Write-Host ""
