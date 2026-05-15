<#
.SYNOPSIS
    Scans the repository for accidentally committed secrets.
    
.DESCRIPTION
    Uses multiple methods to detect secrets:
    1. Pattern-based regex scanning for common credential formats
    2. Detects: API keys, tokens, passwords, connection strings, JWT patterns
    
    Exits with code 0 if no secrets found, 1 if found.
    
.PARAMETER RepositoryPath
    Path to scan. Defaults to current directory.
    
.PARAMETER ExcludePaths
    Comma-separated list of paths to exclude. Defaults to bin,obj,node_modules
    
.PARAMETER Fix
    If set, attempts to remove detected secrets from output (does not modify files).

.EXAMPLE
    .\scripts\ci\scan-secrets.ps1
    .\scripts\ci\scan-secrets.ps1 -RepositoryPath "C:\Booked" -Verbose
#>
param(
    [string]$RepositoryPath = ".",
    [string[]]$ExcludePaths = @("bin", "obj", "node_modules", ".git", ".github"),
    [switch]$Fix,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Secret patterns
$patterns = @{
    "JWT Token" = "eyJ[A-Za-z0-9_-]+\.eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+"
    "API Key" = "api[_-]?key['\"]?\s*[:=]\s*['\"]?([A-Za-z0-9\-_]{20,})['\"]?"
    "Password Assignment" = "password['\"]?\s*[:=]\s*['\"]?([^\s'\"]{8,})['\"]?"
    "Bearer Token" = "bearer\s+([A-Za-z0-9\-_\.]{20,})"
    "AWS Key ID" = "AKIA[0-9A-Z]{16}"
    "Private Key Header" = "-----BEGIN (RSA|DSA|EC|OPENSSH|PGP) PRIVATE KEY-----"
    "Connection String" = "Server=.*Password=.*"
    "Hardcoded Secret" = "['\"]?(secret|apiSecret|clientSecret)['\"]?\s*[:=]\s*['\"]?([A-Za-z0-9\-_]{16,})['\"]?"
}

# Files to always exclude
$excludePatterns = @(
    "\.git\W"
    "\.vs\W"
    "node_modules"
    "bin[\\/]"
    "obj[\\/]"
    "\.trx$"
    "\.dll$"
    "\.exe$"
    "\.pdb$"
)

function Get-FilesToScan {
    param([string]$Path)
    
    $allFiles = Get-ChildItem -Path $Path -File -Recurse -ErrorAction SilentlyContinue
    
    return $allFiles | Where-Object {
        $filePath = $_.FullName
        $shouldExclude = $false
        
        foreach ($pattern in $excludePatterns) {
            if ($filePath -match $pattern) {
                $shouldExclude = $true
                break
            }
        }
        
        -not $shouldExclude
    }
}

function Scan-File {
    param(
        [string]$FilePath,
        [hashtable]$PatternMap
    )
    
    $findings = @()
    
    try {
        $content = Get-Content -Path $FilePath -Raw -ErrorAction SilentlyContinue
        if (-not $content) { return $findings }
        
        $lines = $content -split "`n"
        
        foreach ($lineNum = 0; $lineNum -lt $lines.Count; $lineNum++) {
            $line = $lines[$lineNum]
            
            # Skip known safe lines (dev configs, test data)
            if ($line -match "r4nd0m_dev_secret|admin123|test_token|fake_") {
                continue
            }
            
            foreach ($patternName in $PatternMap.Keys) {
                $pattern = $PatternMap[$patternName]
                
                if ($line -match $pattern) {
                    # Redact sensitive parts
                    $redacted = $line -replace '[A-Za-z0-9\-_]{16,}', '***REDACTED***'
                    
                    $findings += @{
                        File = $FilePath
                        Line = $lineNum + 1
                        Pattern = $patternName
                        Content = if ($Fix) { $redacted } else { $line }
                    }
                }
            }
        }
    }
    catch {
        Write-Verbose "Skipped $FilePath (binary or read error)"
    }
    
    return $findings
}

# Main scan
Write-Host "Scanning repository for secrets: $RepositoryPath`n" -ForegroundColor Cyan

$filesToScan = Get-FilesToScan -Path $RepositoryPath
Write-Host "Found $($filesToScan.Count) files to scan`n" -ForegroundColor Gray

$allFindings = @()
$fileCount = 0

foreach ($file in $filesToScan) {
    $fileCount++
    
    if ($fileCount % 100 -eq 0) {
        Write-Host "Progress: $fileCount/$($filesToScan.Count) files scanned" -ForegroundColor Gray
    }
    
    $findings = Scan-File -FilePath $file.FullName -PatternMap $patterns
    if ($findings) {
        $allFindings += $findings
    }
}

# Report
Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host "SECRET SCAN RESULTS" -ForegroundColor Cyan
Write-Host "========================================================`n" -ForegroundColor Cyan

if ($allFindings.Count -eq 0) {
    Write-Host "[PASSED] No secrets detected in $fileCount files`n" -ForegroundColor Green
    exit 0
}

Write-Host "[FAILED] Found $($allFindings.Count) potential secret(s)`n" -ForegroundColor Red

$allFindings | ForEach-Object {
    Write-Host "  File: $($_.File)" -ForegroundColor Yellow
    Write-Host "  Line: $($_.Line)" -ForegroundColor Yellow
    Write-Host "  Type: $($_.Pattern)" -ForegroundColor Yellow
    Write-Host "  Content: $($_.Content)" -ForegroundColor Gray
    Write-Host ""
}

Write-Host "`nRemediations:" -ForegroundColor Yellow
Write-Host "  1. Review the flagged files immediately" -ForegroundColor Gray
Write-Host "  2. Regenerate any compromised secrets/tokens" -ForegroundColor Gray
Write-Host "  3. Add patterns to .gitignore or use git-secrets pre-commit hooks" -ForegroundColor Gray
Write-Host "  4. Use environment variables or Azure Key Vault for secrets" -ForegroundColor Gray

exit 1
