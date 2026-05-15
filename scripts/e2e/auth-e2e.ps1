<#
.SYNOPSIS
    End-to-end authentication flow test script.
    
.DESCRIPTION
    Tests complete auth flow: register -> login -> refresh -> logout
    Runs multiple cycles to validate token rotation and system stability.
    
.PARAMETER ApiBaseUrl
    Base URL of the Identity API. Default: http://localhost:5154
    
.PARAMETER Cycles
    Number of complete test cycles to run. Default: 3
    
.PARAMETER RefreshesPerCycle
    Number of refresh operations per cycle. Default: 2
    
.EXAMPLE
    .\auth-e2e.ps1 -Cycles 5 -RefreshesPerCycle 3
#>

param(
    [string]$ApiBaseUrl = "http://localhost:5154",
    [int]$Cycles = 3,
    [int]$RefreshesPerCycle = 2
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

# Helper function to make HTTP requests
function Invoke-AuthEndpoint {
    param(
        [string]$Endpoint,
        [object]$Body,
        [string]$Method = "POST"
    )
    
    $url = "$ApiBaseUrl/api/auth$Endpoint"
    $params = @{
        Uri             = $url
        Method          = $Method
        ContentType     = "application/json"
        UseBasicParsing = $true
    }
    
    if ($Body) {
        $params.Body = $Body | ConvertTo-Json -Compress
    }
    
    try {
        $response = Invoke-WebRequest @params
        return @{
            Success  = $true
            Response = $response.Content | ConvertFrom-Json
            StatusCode = $response.StatusCode
        }
    }
    catch {
        $errorResponse = $_.Exception.Response
        if ($errorResponse) {
            $stream = $errorResponse.GetResponseStream()
            $reader = [System.IO.StreamReader]::new($stream)
            $body = $reader.ReadToEnd()
            
            return @{
                Success     = $false
                Response    = $body | ConvertFrom-Json -ErrorAction SilentlyContinue
                StatusCode  = $errorResponse.StatusCode
                ErrorMessage = $_.Exception.Message
            }
        }
        else {
            return @{
                Success      = $false
                ErrorMessage = $_.Exception.Message
            }
        }
    }
}

# Test health endpoint
Write-Information "Testing API health..."
$health = Invoke-AuthEndpoint -Endpoint "/health" -Method GET

if (-not $health.Success) {
    Write-Error "API health check failed. Make sure the Identity API is running at $ApiBaseUrl"
}

Write-Information "✓ API is healthy"
Write-Information ""

# Statistics
$stats = @{
    TotalRegistrations = 0
    SuccessfulRegistrations = 0
    TotalLogins = 0
    SuccessfulLogins = 0
    TotalRefreshes = 0
    SuccessfulRefreshes = 0
    TotalLogouts = 0
    SuccessfulLogouts = 0
    Errors = @()
}

# Run test cycles
Write-Information "Starting E2E test cycles ($Cycles cycles, $RefreshesPerCycle refreshes per cycle)..."
Write-Information ""

for ($cycle = 1; $cycle -le $Cycles; $cycle++) {
    Write-Information "=== CYCLE $cycle/$Cycles ==="
    
    # Generate unique test data
    $guid = [guid]::NewGuid().ToString("N").Substring(0, 8)
    $email = "e2e+$guid@booked.test"
    $password = "P@ssw0rd123!"
    $fullName = "E2E Test User $cycle"
    
    # 1. REGISTER
    Write-Information "  1. Registering user: $email"
    $stats.TotalRegistrations++
    
    $registerBody = @{
        Email = $email
        Password = $password
        FullName = $fullName
    }
    
    $registerResult = Invoke-AuthEndpoint -Endpoint "/customer/register" -Body $registerBody
    
    if ($registerResult.Success -and $registerResult.Response.success) {
        $stats.SuccessfulRegistrations++
        $userId = $registerResult.Response.user.id
        Write-Information "     ✓ Registration successful (ID: $userId)"
    }
    else {
        $errorMsg = "Registration failed: $($registerResult.Response.message)"
        $stats.Errors += $errorMsg
        Write-Error $errorMsg
    }
    
    # 2. LOGIN
    Write-Information "  2. Logging in user"
    $stats.TotalLogins++
    
    $loginBody = @{
        Email = $email
        Password = $password
    }
    
    $loginResult = Invoke-AuthEndpoint -Endpoint "/customer/login" -Body $loginBody
    
    if ($loginResult.Success -and $loginResult.Response.success) {
        $stats.SuccessfulLogins++
        $accessToken = $loginResult.Response.token.accessToken
        $refreshToken = $loginResult.Response.token.refreshToken
        $expiresAt = $loginResult.Response.token.expiresAt
        
        Write-Information "     ✓ Login successful"
        Write-Information "       AccessToken: $($accessToken.Substring(0, 20))..."
        Write-Information "       RefreshToken: $($refreshToken.Substring(0, 20))..."
        Write-Information "       ExpiresAt: $expiresAt"
    }
    else {
        $errorMsg = "Login failed: $($loginResult.Response.message)"
        $stats.Errors += $errorMsg
        Write-Error $errorMsg
    }
    
    # 3. REFRESH (multiple times)
    $previousRefreshToken = $refreshToken
    
    for ($refreshIdx = 1; $refreshIdx -le $RefreshesPerCycle; $refreshIdx++) {
        Write-Information "  3.$refreshIdx Refreshing token (attempt $refreshIdx/$RefreshesPerCycle)"
        $stats.TotalRefreshes++
        
        $refreshBody = @{
            RefreshToken = $previousRefreshToken
        }
        
        $refreshResult = Invoke-AuthEndpoint -Endpoint "/refresh" -Body $refreshBody
        
        if ($refreshResult.Success -and $refreshResult.Response.success) {
            $stats.SuccessfulRefreshes++
            $newAccessToken = $refreshResult.Response.token.accessToken
            $newRefreshToken = $refreshResult.Response.token.refreshToken
            
            Write-Information "       ✓ Refresh successful"
            Write-Information "       New AccessToken: $($newAccessToken.Substring(0, 20))..."
            Write-Information "       New RefreshToken: $($newRefreshToken.Substring(0, 20))..."
            
            # Verify token rotation
            if ($newRefreshToken -eq $previousRefreshToken) {
                $errorMsg = "WARNING: Refresh token was not rotated!"
                $stats.Errors += $errorMsg
                Write-Information "       ⚠ $errorMsg"
            }
            else {
                Write-Information "       ✓ Token rotation confirmed"
            }
            
            $previousRefreshToken = $newRefreshToken
        }
        else {
            $errorMsg = "Refresh failed: $($refreshResult.Response.message)"
            $stats.Errors += $errorMsg
            Write-Error $errorMsg
        }
    }
    
    # 4. LOGOUT
    Write-Information "  4. Logging out user"
    $stats.TotalLogouts++
    
    $logoutBody = @{
        RefreshToken = $previousRefreshToken
    }
    
    $logoutResult = Invoke-AuthEndpoint -Endpoint "/logout" -Body $logoutBody
    
    if ($logoutResult.Success -and $logoutResult.Response.success) {
        $stats.SuccessfulLogouts++
        Write-Information "     ✓ Logout successful"
    }
    else {
        $errorMsg = "Logout failed: $($logoutResult.Response.message)"
        $stats.Errors += $errorMsg
        Write-Error $errorMsg
    }
    
    # 5. VERIFY TOKEN REVOCATION
    Write-Information "  5. Verifying token revocation"
    
    $verifyBody = @{
        RefreshToken = $previousRefreshToken
    }
    
    $verifyResult = Invoke-AuthEndpoint -Endpoint "/refresh" -Body $verifyBody
    
    if (-not $verifyResult.Success -or -not $verifyResult.Response.success) {
        Write-Information "     ✓ Revoked token correctly rejected (expected)"
    }
    else {
        $errorMsg = "ERROR: Revoked token was still accepted!"
        $stats.Errors += $errorMsg
        Write-Error $errorMsg
    }
    
    Write-Information ""
}

# Summary Report
Write-Information "════════════════════════════════════════════════════════"
Write-Information "E2E TEST SUMMARY REPORT"
Write-Information "════════════════════════════════════════════════════════"
Write-Information "API Base URL: $ApiBaseUrl"
Write-Information ""
Write-Information "Registrations: $($stats.SuccessfulRegistrations)/$($stats.TotalRegistrations) successful"
Write-Information "Logins:        $($stats.SuccessfulLogins)/$($stats.TotalLogins) successful"
Write-Information "Refreshes:     $($stats.SuccessfulRefreshes)/$($stats.TotalRefreshes) successful"
Write-Information "Logouts:       $($stats.SuccessfulLogouts)/$($stats.TotalLogouts) successful"
Write-Information ""

if ($stats.Errors.Count -gt 0) {
    Write-Information "Errors and Warnings ($($stats.Errors.Count)):"
    foreach ($error in $stats.Errors) {
        Write-Information "  • $error"
    }
}
else {
    Write-Information "✓ No errors detected"
}

$totalSuccess = ($stats.SuccessfulRegistrations -eq $stats.TotalRegistrations) -and `
                ($stats.SuccessfulLogins -eq $stats.TotalLogins) -and `
                ($stats.SuccessfulRefreshes -eq $stats.TotalRefreshes) -and `
                ($stats.SuccessfulLogouts -eq $stats.TotalLogouts) -and `
                ($stats.Errors.Count -eq 0)

Write-Information ""
if ($totalSuccess) {
    Write-Information "✓ ALL TESTS PASSED"
    exit 0
}
else {
    Write-Information "✗ SOME TESTS FAILED"
    exit 1
}
