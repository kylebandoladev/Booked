namespace Booked.Shared.Contracts.Security;

/// <summary>
/// Rate limiting configuration for protecting authentication endpoints
/// against brute-force attacks and DoS vectors.
/// </summary>
public class RateLimitSettings
{
    /// <summary>
    /// Enable rate limiting globally. Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Rate limit for registration endpoints (per IP).
    /// Default: 5 requests per 15 minutes
    /// </summary>
    public int RegistrationLimit { get; set; } = 5;
    public int RegistrationLimitMinutes { get; set; } = 15;

    /// <summary>
    /// Rate limit for login endpoints (per IP).
    /// Default: 5 requests per 15 minutes
    /// </summary>
    public int LoginLimit { get; set; } = 5;
    public int LoginLimitMinutes { get; set; } = 15;

    /// <summary>
    /// Rate limit for token refresh endpoint (per IP).
    /// Default: 10 requests per 1 minute (legitimate clients need frequent refresh)
    /// </summary>
    public int RefreshLimit { get; set; } = 10;
    public int RefreshLimitMinutes { get; set; } = 1;

    /// <summary>
    /// HTTP status code returned when rate limit exceeded.
    /// Default: 429 (Too Many Requests)
    /// </summary>
    public int HttpStatusCode { get; set; } = 429;

    /// <summary>
    /// Enable detailed logging for rate limit violations.
    /// Default: true
    /// </summary>
    public bool LogViolations { get; set; } = true;
}
