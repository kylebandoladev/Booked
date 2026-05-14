namespace Booked.Identity.Domain;

public sealed class RefreshToken
{
    public required string Id { get; init; }
    public required string Token { get; init; }
    public required string Subject { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public required string IpAddress { get; init; }
    public required string UserAgent { get; init; }
}
