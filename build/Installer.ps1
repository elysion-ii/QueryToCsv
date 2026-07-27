# QueryToCsv Installer Build Script

$BuildDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionDir = Split-Path -Parent $BuildDir
Set-Location $BuildDir

# Check Inno Setup installation
$IsccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

if (-not (Test-Path $IsccPath)) {
    Write-Host "   [ERROR] Inno Setup not found" -ForegroundColor Red
    Write-Host "   Install Inno Setup 6 or later from https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    exit 1
}

# Read the app version from Directory.Build.props (single source of truth).
# The .iss script has no version of its own; it is injected below via /D.
$PropsPath = Join-Path $SolutionDir "Directory.Build.props"
if (-not (Test-Path $PropsPath)) {
    Write-Host "   [ERROR] Directory.Build.props not found at $PropsPath" -ForegroundColor Red
    exit 1
}
$PropsXml = [xml](Get-Content $PropsPath -Raw)
$AppVersion = ($PropsXml.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1
if (-not $AppVersion) {
    Write-Host "   [ERROR] <Version> not found in Directory.Build.props" -ForegroundColor Red
    Write-Host "   Directory.Build.props is the single source of truth for the app version" -ForegroundColor Yellow
    exit 1
}
Write-Host "App version: $AppVersion (from Directory.Build.props)" -ForegroundColor Cyan

# Release gate: if a changelog exists, it must have an entry for this version
$ChangelogPath = Join-Path $SolutionDir "CHANGELOG.md"
if (Test-Path $ChangelogPath) {
    $VersionPattern = "^##\s*\[?" + [regex]::Escape($AppVersion) + "\]?(\s|$)"
    $Entry = Select-String -Path $ChangelogPath -Pattern $VersionPattern
    if (-not $Entry) {
        Write-Host "   [ERROR] CHANGELOG.md has no entry for version $AppVersion" -ForegroundColor Red
        Write-Host "   Add a '## [$AppVersion]' section before building the installer" -ForegroundColor Yellow
        exit 1
    }
}

# Create Installer output folder
$InstallerDir = Join-Path $BuildDir "Installer"
if (-not (Test-Path $InstallerDir)) {
    New-Item -ItemType Directory -Path $InstallerDir -Force | Out-Null
}

Write-Host "`n=== Building QueryToCsv Installer ===" -ForegroundColor Green

# Check if build output exists
$ExePath = Join-Path $BuildDir "QueryToCsv\QueryToCsv.exe"
if (-not (Test-Path $ExePath)) {
    Write-Host "   [ERROR] QueryToCsv.exe not found in build\QueryToCsv\" -ForegroundColor Red
    Write-Host "   Run 'Build' (option 1) first" -ForegroundColor Yellow
    exit 1
}

# Build installer
Write-Host "Building installer..." -ForegroundColor Cyan
$SetupScriptPath = Join-Path $BuildDir "Setup_QueryToCsv.iss"

if (-not (Test-Path $SetupScriptPath)) {
    Write-Host "   [ERROR] Setup script 'Setup_QueryToCsv.iss' not found." -ForegroundColor Red
    exit 1
}

& $IsccPath "/DMyAppVersion=$AppVersion" $SetupScriptPath

if ($LASTEXITCODE -eq 0) {
    Write-Host "   [OK] Installer created successfully" -ForegroundColor Green
    Write-Host "`n=== Installer Build Completed ===" -ForegroundColor Green
    Write-Host "`nOutput location: build\Installer\" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host "   [ERROR] Installer build failed" -ForegroundColor Red
    Write-Host "`n=== Installer Build Failed ===" -ForegroundColor Red
    exit 1
}
