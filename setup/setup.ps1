<#
.SYNOPSIS
    Bibliotheca LMS - one-step install and run script (Windows / PowerShell).

.DESCRIPTION
    Clones (or updates) the Bibliotheca repository, switches to the chosen location branch,
    restores packages, builds, prepares the SQLite database and runs the site.
    Every library location follows the same steps - only -Branch (and optionally -Location) changes.

.PARAMETER RepoUrl
    Git URL of the team repository. Not needed when the script is run from inside an existing clone.

.PARAMETER Branch
    Git branch to run (e.g. main, or a location branch such as location/palmerston). Default: main.

.PARAMETER InstallDir
    Folder to clone into. Default: <your user folder>\Bibliotheca

.PARAMETER Location
    Name of the library location shown on the self-service kiosk (e.g. "Palmerston Library").

.PARAMETER Port
    Local port for the website. Default: 5226

.PARAMETER ResetDatabase
    Deletes the local database so it is recreated and re-seeded with demo data on startup.

.PARAMETER NoRun
    Install and build only; do not start the website.

.EXAMPLE
    .\setup.ps1 -RepoUrl https://github.com/<team>/<repo>.git
.EXAMPLE
    .\setup.ps1 -RepoUrl https://github.com/<team>/<repo>.git -Branch location/palmerston -Location "Palmerston Library" -ResetDatabase
#>
[CmdletBinding()]
param(
    [string]$RepoUrl = "",
    [string]$Branch = "main",
    [string]$InstallDir = (Join-Path $HOME "Bibliotheca"),
    [string]$Location = "",
    [int]$Port = 5226,
    [switch]$ResetDatabase,
    [switch]$NoRun
)

$ErrorActionPreference = "Stop"
$RequiredSdkMajor = 10

function Write-Step($n, $text) { Write-Host ""; Write-Host "[$n] $text" -ForegroundColor Cyan }
function Write-Ok($text)       { Write-Host "    OK  $text" -ForegroundColor Green }
function Write-Warn2($text)    { Write-Host "    !!  $text" -ForegroundColor Yellow }
function Fail($text) {
    Write-Host ""
    Write-Host "SETUP STOPPED: $text" -ForegroundColor Red
    exit 1
}
function Invoke-Checked([string]$exe, [string[]]$arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { Fail "'$exe $($arguments -join ' ')' failed (exit code $LASTEXITCODE). See the messages above." }
}

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  Bibliotheca LMS - Install & Run" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

# ---------------------------------------------------------------------------
Write-Step 1 "Checking required software"
# ---------------------------------------------------------------------------
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Fail "Git is not installed. Install it with:  winget install --id Git.Git -e   (then open a NEW PowerShell window)"
}
Write-Ok ("Git " + ((git --version) -replace 'git version ', ''))

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Fail ".NET SDK is not installed. Install it with:  winget install --id Microsoft.DotNet.SDK.$RequiredSdkMajor -e   (then open a NEW PowerShell window)"
}
$sdks = @(dotnet --list-sdks 2>$null)
$hasSdk = $sdks | Where-Object { $_ -match "^$RequiredSdkMajor\." }
if (-not $hasSdk) {
    Write-Host "    Installed SDKs:`n      $($sdks -join "`n      ")"
    Fail ".NET $RequiredSdkMajor SDK not found. Install it with:  winget install --id Microsoft.DotNet.SDK.$RequiredSdkMajor -e"
}
Write-Ok (".NET SDK " + (($hasSdk | Select-Object -Last 1) -split ' ')[0])

# ---------------------------------------------------------------------------
Write-Step 2 "Getting the source code"
# ---------------------------------------------------------------------------
# If the script is run from inside an existing clone, use that clone.
$scriptRepo = $null
try { $scriptRepo = (git -C $PSScriptRoot rev-parse --show-toplevel 2>$null) } catch { }
if ($scriptRepo -and -not $RepoUrl) {
    $RepoRoot = $scriptRepo
    Write-Ok "Using existing clone at $RepoRoot"
}
elseif (Test-Path (Join-Path $InstallDir ".git")) {
    $RepoRoot = (Resolve-Path $InstallDir).Path
    Write-Ok "Repository already cloned at $RepoRoot"
}
else {
    if (-not $RepoUrl) { Fail "Please give the repository URL, e.g.  .\setup.ps1 -RepoUrl https://github.com/<team>/<repo>.git" }
    Write-Host "    Cloning $RepoUrl into $InstallDir ..."
    Invoke-Checked git @("clone", $RepoUrl, $InstallDir)
    $RepoRoot = (Resolve-Path $InstallDir).Path
    Write-Ok "Cloned"
}

# ---------------------------------------------------------------------------
Write-Step 3 "Selecting branch '$Branch'"
# ---------------------------------------------------------------------------
$dirty = git -C $RepoRoot status --porcelain --untracked-files=no
if ($dirty) { Fail "You have uncommitted changes in $RepoRoot. Commit or stash them first (git stash), then run setup again." }

Invoke-Checked git @("-C", $RepoRoot, "fetch", "--all", "--prune")
$remoteBranch = git -C $RepoRoot branch -r --list "origin/$Branch"
$localBranch  = git -C $RepoRoot branch --list $Branch
if (-not $remoteBranch -and -not $localBranch) {
    Write-Host "    Available branches:"
    git -C $RepoRoot branch -r | ForEach-Object { Write-Host "      $_" }
    Fail "Branch '$Branch' does not exist. Choose one of the branches listed above."
}
Invoke-Checked git @("-C", $RepoRoot, "switch", $Branch)
if ($remoteBranch) { Invoke-Checked git @("-C", $RepoRoot, "pull", "--ff-only") }
Write-Ok ("On branch $Branch at commit " + (git -C $RepoRoot rev-parse --short HEAD))

# ---------------------------------------------------------------------------
Write-Step 4 "Locating the web project"
# ---------------------------------------------------------------------------
$csproj = Get-ChildItem -Path $RepoRoot -Recurse -Filter "Bibliotheca.csproj" -File |
          Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | Select-Object -First 1
if (-not $csproj) { Fail "Bibliotheca.csproj was not found in $RepoRoot." }
$ProjectDir = $csproj.DirectoryName
Write-Ok $csproj.FullName

# ---------------------------------------------------------------------------
Write-Step 5 "Restoring NuGet packages and building"
# ---------------------------------------------------------------------------
Invoke-Checked dotnet @("restore", $csproj.FullName)
Invoke-Checked dotnet @("build", $csproj.FullName, "--no-restore", "-c", "Debug", "-v", "minimal")
Write-Ok "Build succeeded"

# ---------------------------------------------------------------------------
Write-Step 6 "Preparing the database"
# ---------------------------------------------------------------------------
$dbFiles = Get-ChildItem -Path $ProjectDir -Filter "bibliotheca.db*" -File -ErrorAction SilentlyContinue
if ($ResetDatabase -and $dbFiles) {
    Get-Process -Name "Bibliotheca" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    $dbFiles | Remove-Item -Force
    Write-Ok "Old database deleted - a fresh one will be created and seeded"
}
elseif ($dbFiles) {
    Write-Ok "Existing database kept (use -ResetDatabase for a clean, freshly seeded copy)"
}
else {
    Write-Ok "No database yet - it will be created and seeded automatically on first start"
}

if (Test-Path (Join-Path $ProjectDir "Migrations")) {
    Write-Host "    EF Core migrations found - applying them ..."
    if (-not (dotnet tool list -g | Select-String "dotnet-ef")) {
        Invoke-Checked dotnet @("tool", "install", "--global", "dotnet-ef", "--version", "$RequiredSdkMajor.*")
        $env:PATH += ";$HOME\.dotnet\tools"
    }
    Invoke-Checked dotnet @("ef", "database", "update", "--project", $csproj.FullName)
    Write-Ok "Migrations applied"
}

# ---------------------------------------------------------------------------
Write-Step 7 "Ready"
# ---------------------------------------------------------------------------
$base = "http://localhost:$Port"
Write-Host ""
Write-Host "  Public catalogue      $base/" -ForegroundColor White
Write-Host "  Self-service kiosk    $base/Kiosk" -ForegroundColor White
Write-Host "  Staff login           $base/Account/Login" -ForegroundColor White
Write-Host ""
Write-Host "  Staff accounts: admin@bibliotheca.com / Admin@123"
Write-Host "                  reception@bibliotheca.com / Reception@123"
Write-Host "                  manager@bibliotheca.com / Manager@123"
Write-Host "  Kiosk test cards: CRD-1001 ... CRD-1004"
if ($Location) { Write-Host "  Kiosk location name: $Location" }
Write-Host ""

if ($NoRun) {
    Write-Host "Setup finished. Start the site later with:" -ForegroundColor Green
    Write-Host "  cd `"$ProjectDir`"; dotnet run --launch-profile http -- --urls $base"
    exit 0
}

Write-Host "Starting the website - press Ctrl+C to stop it." -ForegroundColor Green
$runArgs = @("run", "--no-build", "--launch-profile", "http", "--", "--urls", $base)
if ($Location) { $runArgs += "--Kiosk:BranchName=$Location" }

Push-Location $ProjectDir   # the SQLite file lives next to the project
try {
    & dotnet @runArgs
}
finally {
    Pop-Location
}
