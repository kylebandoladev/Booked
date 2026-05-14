using Booked.Identity.Domain;

namespace Booked.Identity.Application;

public interface IRefreshTokenService
{
    Task<RefreshToken> CreateAsync(string subject, string token, DateTime expiresAt, string ipAddress, string userAgent);
    Task<RefreshToken?> GetAsync(string token);
    Task<RefreshToken?> GetBySubjectAsync(string subject);
    Task RevokeAsync(string token);
    Task RevokeBySubjectAsync(string subject);
    Task CleanupExpiredAsync();
}
