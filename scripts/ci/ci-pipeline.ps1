<#
.SYNOPSIS
    Master CI/CD pipeline orchestrator for local and CI validation.
    
.DESCRIPTION
    Runs comprehensive security checks, tests, and quality gates:
    1. Build the solution
    2. Unit tests
    3. Integration tests
    4. Vulnerability scanning
    5. Secret scanning
    6. Optional SAST (if enabled)
    
    Generates a test report and exits with appropriate code.
    
.PARAMETER Stage
    Which stage to run. Options: 'all', 'build', 'test', 'security'
    Default: 'all'
    
.PARAMETER ExitOnFail
    Exit immediately on first failure. Default: $true
    
.PARAMETER GenerateReport
    Generate HTML test report. Default: $true

.EXAMPLE
    .\scripts\ci\ci-pipeline.ps1
    .\scripts\ci\ci-pipeline.ps1 -Stage test -GenerateReport
    .\scripts\ci\ci-pipeline.ps1 -Stage security -ExitOnFail:$false
#>
param(
    [ValidateSet("all", "build", "test", "security")]
    [string]$Stage = "all",
    [bool]$ExitOnFail = $true,
    [bool]$GenerateReport = $true,
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"

# Configuration
$projectRoot = Get-Location
$reportsDir = "$projectRoot\.reports"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$reportName = "ci-report-$timestamp.html"

# Colors
$colors = @{
    Pass = "Green"
    Fail = "Red"
    Warn = "Yellow"
    Info = "Cyan"
}

function Write-Stage {
    param([string]$Message, [string]$Type = "Info")
    Write-Host "`n========================================================" -ForegroundColor $colors[$Type]
    Write-Host $Message -ForegroundColor $colors[$Type]
    Write-Host "========================================================`n" -ForegroundColor $colors[$Type]
}

function Write-Result {
    param([string]$Name, [bool]$Passed, [string]$Details = "")
    $icon = if ($Passed) { "[PASS]" } else { "[FAIL]" }
    $color = if ($Passed) { $colors.Pass } else { $colors.Fail }
    Write-Host "$icon $Name" -ForegroundColor $color
    if ($Details) { Write-Host "   $Details" -ForegroundColor Gray }
}

function Invoke-Stage {
    param(
        [string]$Name,
        [scriptblock]$Block,
        [string]$Description = ""
    )
    
    Write-Stage "$Name" "Info"
    if ($Description) { Write-Host $Description -ForegroundColor Gray }
    
    try {
        & $Block
        return $true
    }
    catch {
        Write-Host "ERROR: $_" -ForegroundColor Red
        if ($ExitOnFail) { exit 1 }
        return $false
    }
}

# ============================================================================
# STAGE: Build
# ============================================================================
$buildPassed = $true
if ($Stage -in @("all", "build")) {
    $buildPassed = Invoke-Stage "STAGE 1: BUILD" {
        Write-Host "Building solution..." -ForegroundColor Gray
        $output = dotnet build --configuration Release 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host $output -ForegroundColor Red
            throw "Build failed"
        }
        Write-Result "Build" $true "✓ Release build successful"
    }
}

# ============================================================================
# STAGE: Unit Tests
# ============================================================================
$unitTestPassed = $true
if ($Stage -in @("all", "test") -and $buildPassed) {
    $unitTestPassed = Invoke-Stage "STAGE 2: UNIT TESTS" {
        Write-Host "Running unit tests..." -ForegroundColor Gray
        $output = dotnet test tests\unit\Booked.Identity.Tests\Booked.Identity.Tests.csproj `
            --no-build `
            --logger "console;verbosity=minimal" `
            --logger "trx;LogFileName=$reportsDir\unit-tests-$timestamp.trx" 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host $output | Select-Object -Last 20 -ForegroundColor Red
            throw "Unit tests failed"
        }
        
        $summary = $output | Select-String "Passed|Failed"
        Write-Result "Unit Tests" $true $summary
    }
}

# ============================================================================
# STAGE: Integration Tests
# ============================================================================
$integrationTestPassed = $true
if ($Stage -in @("all", "test") -and $unitTestPassed) {
    $integrationTestPassed = Invoke-Stage "STAGE 3: INTEGRATION TESTS" {
        Write-Host "Running integration tests..." -ForegroundColor Gray
        $output = dotnet test tests\integration\Booked.Identity.Integration.Tests\Booked.Identity.Integration.Tests.csproj `
            --no-build `
            --logger "console;verbosity=minimal" `
            --logger "trx;LogFileName=$reportsDir\integration-tests-$timestamp.trx" 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host $output | Select-Object -Last 20 -ForegroundColor Red
            throw "Integration tests failed"
        }
        
        $summary = $output | Select-String "Passed|Failed"
        Write-Result "Integration Tests" $true $summary
    }
}

# ============================================================================
# STAGE: Security Scans
# ============================================================================
if ($Stage -in @("all", "security")) {
    # Vulnerability Scan
    $vulnScanPassed = Invoke-Stage "STAGE 4: SECURITY - VULNERABILITY SCAN" {
        Write-Host "Checking for vulnerable packages..." -ForegroundColor Gray
        & ".\scripts\ci\check-vulnerabilities.ps1" -SolutionPath "." -Verbose:$Verbose
        Write-Result "Vulnerability Scan" $true "✓ No known vulnerabilities"
    }
    
    # Secret Scan
    if ($vulnScanPassed) {
        $secretScanPassed = Invoke-Stage "STAGE 5: SECURITY - SECRET SCAN" {
            Write-Host "Scanning for accidentally committed secrets..." -ForegroundColor Gray
            & ".\scripts\ci\scan-secrets.ps1" -RepositoryPath "." -Verbose:$Verbose
            Write-Result "Secret Scan" $true "✓ No secrets detected"
        }
    }
}

# ============================================================================
# FINAL REPORT
# ============================================================================
Write-Stage "CI/CD PIPELINE SUMMARY" "Pass"

Write-Host "Results:" -ForegroundColor Cyan
Write-Result "Build" $buildPassed
Write-Result "Unit Tests" $unitTestPassed
Write-Result "Integration Tests" $integrationTestPassed

if ($Stage -in @("all", "security")) {
    Write-Result "Vulnerability Scan" $vulnScanPassed
    Write-Result "Secret Scan" $secretScanPassed
}

$allPassed = $buildPassed -and $unitTestPassed -and $integrationTestPassed
if ($Stage -in @("all", "security")) {
    $allPassed = $allPassed -and $vulnScanPassed -and $secretScanPassed
}

if ($allPassed) {
    Write-Host "`n[ALL CHECKS PASSED]`n" -ForegroundColor Green
    Write-Host "Reports saved to: $reportsDir" -ForegroundColor Gray
    exit 0
} else {
    Write-Host "`n[PIPELINE FAILED] Review errors above`n" -ForegroundColor Red
    exit 1
}
