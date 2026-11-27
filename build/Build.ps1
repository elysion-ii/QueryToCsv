# QueryToCsv Build Script

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

# Build folder is the current location, solution root is parent
$BuildDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionDir = Split-Path -Parent $BuildDir
Set-Location $SolutionDir

$Framework = "net10.0"
$OutputDir = Join-Path $BuildDir "QueryToCsv"
$PublishDir = Join-Path $BuildDir "publish_temp"
$ProjectPath = "QueryToCsv\QueryToCsv.csproj"

# Check dotnet CLI is available
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "   [ERROR] dotnet CLI not found. Please install .NET SDK." -ForegroundColor Red
    exit 1
}

Write-Host "`n=== Building QueryToCsv ===" -ForegroundColor Green

if (-not (Test-Path $ProjectPath)) {
    Write-Host "   [ERROR] QueryToCsv project not found at $ProjectPath" -ForegroundColor Red
    exit 1
}

# Cleanup temp folder
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
# Clean existing output folder
if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }

# Create output folder
New-Item -ItemType Directory -Path $OutputDir -Force -ErrorAction SilentlyContinue | Out-Null

# Only the placeholder template of a configuration file belongs in the repository:
# the real file carries credentials and environment-specific values
# (rule: docs/rules/dotnet.md, CONFIGFILE)
Write-Host "Verifying configuration files..." -ForegroundColor Cyan
# Configuration extensions only, so a source-code template never trips the check
$ConfigTemplate = '\.template\.(json|ya?ml|xml|ini|config|toml|env)$'
$TrackedFiles = @()
if (Get-Command git -ErrorAction SilentlyContinue) {
    $TrackedFiles = @(git ls-files 2>$null)
    if ($LASTEXITCODE -ne 0) { $TrackedFiles = @() }
}
foreach ($Template in $TrackedFiles | Where-Object { $_ -match $ConfigTemplate }) {
    $RealName = $Template -replace $ConfigTemplate, '.$1'
    if ($TrackedFiles -contains $RealName) {
        Write-Host "   [ERROR] $RealName is tracked by git - only $Template belongs in the repository" -ForegroundColor Red
        Write-Host "           Run 'git rm --cached $RealName' and add it to .gitignore" -ForegroundColor Red
        Write-Host "`n=== Build Failed ===" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Verifying code format..." -ForegroundColor Cyan
dotnet format QueryToCsv.slnx --verify-no-changes
if ($LASTEXITCODE -ne 0) {
    Write-Host "   [ERROR] Unformatted code detected - run 'dotnet format QueryToCsv.slnx' and rebuild" -ForegroundColor Red
    Write-Host "`n=== Build Failed ===" -ForegroundColor Red
    exit 1
}

# Every requirement a specification states is named by a test, and no test names a
# requirement no specification states
# (rules: docs/rules/testing.md, Requirement Coverage; docs/rules/dotnet.md, REQCOV)
Write-Host "Verifying requirement coverage..." -ForegroundColor Cyan
# IDs compare exactly, case included (docs/rules/documentation.md, Requirement IDs)
$RequirementHeading = '(?m)^\s*#{1,6}\s+([A-Z][A-Z0-9]*-\d+):'
$RequirementIds = @()
if (Test-Path "docs\specs") {
    # An HTML comment is not part of the specification (e.g., the skeleton's example heading)
    $RequirementIds = @(Get-ChildItem "docs\specs" -Recurse -Filter *.md |
        ForEach-Object { [regex]::Replace([System.IO.File]::ReadAllText($_.FullName), '(?s)<!--.*?-->', '') } |
        ForEach-Object { [regex]::Matches($_, $RequirementHeading) } |
        ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
}
$TestSource = (Get-ChildItem -Directory | Where-Object { $_.Name -like "*Tests" } |
    Get-ChildItem -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    ForEach-Object { [System.IO.File]::ReadAllText($_.FullName) }) -join "`n"
$Unnamed = @($RequirementIds | Where-Object {
    $TestSource -cnotmatch ('(?<![A-Za-z0-9])' + [regex]::Escape($_) + '(?![0-9])')
})
$TraitIds = @([regex]::Matches($TestSource, 'Trait\(\s*"Requirement"\s*,\s*"([^"]*)"\s*\)') |
    ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
$Missing = @($TraitIds | Where-Object { $RequirementIds -cnotcontains $_ })
if ($Unnamed.Count -gt 0 -or $Missing.Count -gt 0) {
    if ($Unnamed.Count -gt 0) {
        Write-Host "   [ERROR] No test names these requirements: $($Unnamed -join ', ')" -ForegroundColor Red
    }
    if ($Missing.Count -gt 0) {
        Write-Host "   [ERROR] Tests name requirements no specification states: $($Missing -join ', ')" -ForegroundColor Red
    }
    Write-Host "           See docs/rules/dotnet.md, REQCOV" -ForegroundColor Red
    Write-Host "`n=== Build Failed ===" -ForegroundColor Red
    exit 1
}

Write-Host "Running QueryToCsv tests..." -ForegroundColor Cyan
dotnet test QueryToCsv.Tests\QueryToCsv.Tests.csproj -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "   [ERROR] Tests failed - build aborted" -ForegroundColor Red
    if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
    Write-Host "`n=== Build Failed ===" -ForegroundColor Red
    exit 1
}

Write-Host "Building QueryToCsv..." -ForegroundColor Cyan
dotnet publish $ProjectPath `
    -c $Configuration `
    -f $Framework `
    -r $Runtime `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:DebuggerSupport=false `
    -o "$PublishDir"

if ($LASTEXITCODE -eq 0) {
    Copy-Item "$PublishDir\*" "$OutputDir\" -Force -Recurse

    # The installer ships appsettings.template.json as the initial appsettings.json
    $ConfigTemplateFile = Join-Path $SolutionDir "QueryToCsv\appsettings.template.json"
    if (Test-Path $ConfigTemplateFile) {
        Copy-Item $ConfigTemplateFile (Join-Path $OutputDir "appsettings.json") -Force
        Write-Host "   [OK] appsettings.json created from template" -ForegroundColor Green
    } else {
        Write-Host "   [WARN] appsettings.template.json not found - appsettings.json will be missing" -ForegroundColor Yellow
    }

    # The installer creates these at the install target; they are also needed to run from build output
    $QueriesDir = Join-Path $OutputDir "queries"
    $OutputCsvDir = Join-Path $OutputDir "output"
    New-Item -ItemType Directory -Path $QueriesDir -Force -ErrorAction SilentlyContinue | Out-Null
    New-Item -ItemType Directory -Path $OutputCsvDir -Force -ErrorAction SilentlyContinue | Out-Null
    Write-Host "   [OK] queries and output folders created" -ForegroundColor Green

    Remove-Item $PublishDir -Recurse -Force

    Write-Host "   [OK] QueryToCsv.exe deployed" -ForegroundColor Green
    Write-Host "`n   Output: $OutputDir" -ForegroundColor Cyan
    Write-Host "`n=== Build Completed Successfully ===" -ForegroundColor Green
    exit 0
} else {
    Write-Host "   [ERROR] QueryToCsv build failed" -ForegroundColor Red
    if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
    Write-Host "`n=== Build Failed ===" -ForegroundColor Red
    exit 1
}
