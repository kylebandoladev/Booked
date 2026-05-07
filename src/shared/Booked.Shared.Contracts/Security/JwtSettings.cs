namespace Booked.Shared.Contracts.Security;

public sealed class JwtSettings
{
    // Secret used to sign tokens (HMAC). Keep this secret in production secrets store.
    public string Secret { get; set; } = string.Empty;

    // Token issuer
    public string Issuer { get; set; } = "booked";

    // Expected audience
    public string Audience { get; set; } = "booked_clients";

    // Access token lifetime in minutes
    public int AccessTokenExpirationMinutes { get; set; } = 60;

    // Refresh token lifetime in days
    public int RefreshTokenExpirationDays { get; set; } = 7;
}