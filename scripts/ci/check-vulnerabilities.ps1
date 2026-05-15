<#
.SYNOPSIS
    Scans the Booked solution for vulnerable NuGet packages.
    
.DESCRIPTION
    Uses 'dotnet list package --vulnerable' to identify any known vulnerabilities
    in project dependencies. Exits with code 0 if no vulnerabilities, 1 if found.
    
.PARAMETER SolutionPath
    Path to the .sln file. Defaults to current directory if not specified.
    
.PARAMETER Verbose
    Show detailed output for each project checked.

.EXAMPLE
    .\scripts\ci\check-vulnerabilities.ps1
    .\scripts\ci\check-vulnerabilities.ps1 -SolutionPath "C:\Booked\Booked.sln" -Verbose
#>
param(
    [string]$SolutionPath = ".",
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Colors for output
$colors = @{
    Error = "Red"
    Warning = "Yellow"
    Success = "Green"
    Info = "Cyan"
}

function Write-Color {
    param(
        [string]$Message,
        [ValidateSet("Error", "Warning", "Success", "Info")][string]$Type = "Info"
    )
    Write-Host $Message -ForegroundColor $colors[$Type]
}

function Scan-Project {
    param([string]$ProjectPath)
    
    Write-Color "Scanning: $ProjectPath" "Info"
    
    $output = dotnet list $ProjectPath package --vulnerable 2>&1
    $lines = $output | Where-Object { $_ }
    
    $vulnerable = $lines | Where-Object { $_ -match "^>" -or $_ -match "^\s+>" }
    
    if ($Verbose) {
        $lines | ForEach-Object { Write-Host "  $_" }
    }
    
    if ($vulnerable) {
        Write-Color "  [WARNING] VULNERABLE PACKAGES FOUND" "Warning"
        $vulnerable | ForEach-Object { Write-Host "     $_" -ForegroundColor Yellow }
        return $true
    } else {
        Write-Color "  [OK] No vulnerabilities detected" "Success"
        return $false
    }
}

# Find all .csproj files
Write-Color "Searching for .csproj files in: $SolutionPath" "Info"
$projects = Get-ChildItem -Path $SolutionPath -Filter "*.csproj" -Recurse | Where-Object {
    $_.FullName -notmatch "(\\bin\\|\\obj\\)" -and $_.FullName -notmatch "node_modules"
}

if ($projects.Count -eq 0) {
    Write-Color "ERROR: No .csproj files found" "Error"
    exit 1
}

Write-Color "`nFound $($projects.Count) project(s) to scan`n" "Info"

$foundVulnerabilities = $false
$resultsTable = @()

foreach ($project in $projects) {
    $projectName = $project.Name -replace "\.csproj$", ""
    $hasVuln = Scan-Project $project.FullName
    
    $resultsTable += @{
        Project = $projectName
        Status = if ($hasVuln) { "VULNERABLE" } else { "CLEAN" }
    }
    
    if ($hasVuln) {
        $foundVulnerabilities = $true
    }
}

# Summary
Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host "VULNERABILITY SCAN SUMMARY" -ForegroundColor Cyan
Write-Host "========================================================`n" -ForegroundColor Cyan

$resultsTable | Format-Table -AutoSize

if ($foundVulnerabilities) {
    Write-Color "`n[FAILED] Vulnerable packages detected" "Error"
    Write-Color "`nRemediations:" "Warning"
    Write-Color "  1. Update packages: dotnet package update" "Info"
    Write-Color "  2. Check advisories at: https://nvd.nist.gov" "Info"
    Write-Color "  3. Update package references to secure versions" "Info"
    exit 1
} else {
    Write-Color "`n[PASSED] No vulnerable packages detected" "Success"
    exit 0
}
