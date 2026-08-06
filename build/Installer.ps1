# QueryToCsv Installer Build Script

$BuildDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionDir = Split-Path -Parent $BuildDir
Set-Location $BuildDir

# Inno Setup installs per-machine or per-user depending on elevation, so its location
# is discovered rather than assumed.
function Resolve-IsccPath {
    $Candidates = New-Object System.Collections.Generic.List[string]

    # Every installation writes an uninstall entry, in the scope it was installed to.
    $UninstallRoots = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
    )
    Get-ChildItem $UninstallRoots -ErrorAction SilentlyContinue |
        ForEach-Object { Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue } |
        Where-Object { $_.DisplayName -like "*Inno Setup*" -and $_.InstallLocation } |
        Sort-Object @{ Expression = { $_.DisplayVersion -as [version] }; Descending = $true } |
        ForEach-Object { $Candidates.Add((Join-Path $_.InstallLocation "ISCC.exe")) }

    $OnPath = Get-Command ISCC.exe -CommandType Application -ErrorAction SilentlyContinue
    if ($OnPath) { $Candidates.Add($OnPath.Source) }

    # Compil32.exe sits next to ISCC.exe.
    $Association = (Get-ItemProperty "HKLM:\SOFTWARE\Classes\InnoSetupScriptFile\shell\Compile\command" -ErrorAction SilentlyContinue).'(default)'
    if ($Association -match '"([^"]+)"') {
        $Candidates.Add((Join-Path (Split-Path $Matches[1] -Parent) "ISCC.exe"))
    }

    foreach ($Base in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, "$env:LOCALAPPDATA\Programs")) {
        if (-not $Base) { continue }
        foreach ($Major in 7, 6, 5) {
            $Candidates.Add((Join-Path $Base "Inno Setup $Major\ISCC.exe"))
        }
    }

    $Candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}

$IsccPath = Resolve-IsccPath

if (-not $IsccPath) {
    Write-Host "   [ERROR] Inno Setup not found" -ForegroundColor Red
    Write-Host "   Searched the uninstall registry, PATH, the .iss file association, and the default install locations" -ForegroundColor Yellow
    Write-Host "   Install Inno Setup 6 or later from https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    exit 1
}
Write-Host "Inno Setup: $IsccPath" -ForegroundColor Cyan

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
