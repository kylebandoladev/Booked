using Booked.Identity.Application;
using Booked.Identity.Domain;
using System.Collections.Concurrent;

namespace Booked.Identity.Infrastructure.Services;

public sealed class InMemoryRefreshTokenService : IRefreshTokenService
{
    private static readonly ConcurrentDictionary<string, RefreshToken> TokenStore = new();

    public Task<RefreshToken> CreateAsync(string subject, string token, DateTime expiresAt, string ipAddress, string userAgent)
    {
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid().ToString(),
            Token = token,
            Subject = subject,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            IsRevoked = false,
            RevokedAt = null,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        TokenStore.AddOrUpdate(token, refreshToken, (_, _) => refreshToken);
        return Task.FromResult(refreshToken);
    }

    public Task<RefreshToken?> GetAsync(string token)
    {
        TokenStore.TryGetValue(token, out var refreshToken);
        return Task.FromResult(refreshToken);
    }

    public Task<RefreshToken?> GetBySubjectAsync(string subject)
    {
        var refreshToken = TokenStore.Values.FirstOrDefault(rt => rt.Subject == subject && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow);
        return Task.FromResult(refreshToken);
    }

    public Task RevokeAsync(string token)
    {
        if (TokenStore.TryGetValue(token, out var refreshToken))
        {
            var revoked = new RefreshToken
            {
                Id = refreshToken.Id,
                Token = refreshToken.Token,
                Subject = refreshToken.Subject,
                CreatedAt = refreshToken.CreatedAt,
                ExpiresAt = refreshToken.ExpiresAt,
                IsRevoked = true,
                RevokedAt = DateTime.UtcNow,
                IpAddress = refreshToken.IpAddress,
                UserAgent = refreshToken.UserAgent
            };
            TokenStore.AddOrUpdate(token, revoked, (_, _) => revoked);
        }

        return Task.CompletedTask;
    }

    public Task RevokeBySubjectAsync(string subject)
    {
        var tokensToRevoke = TokenStore.Values.Where(rt => rt.Subject == subject && !rt.IsRevoked).ToList();
        foreach (var token in tokensToRevoke)
        {
            var revoked = new RefreshToken
            {
                Id = token.Id,
                Token = token.Token,
                Subject = token.Subject,
                CreatedAt = token.CreatedAt,
                ExpiresAt = token.ExpiresAt,
                IsRevoked = true,
                RevokedAt = DateTime.UtcNow,
                IpAddress = token.IpAddress,
                UserAgent = token.UserAgent
            };
            TokenStore.AddOrUpdate(token.Token, revoked, (_, _) => revoked);
        }

        return Task.CompletedTask;
    }

    public Task CleanupExpiredAsync()
    {
        var expiredTokens = TokenStore.Where(x => x.Value.ExpiresAt < DateTime.UtcNow).ToList();
        foreach (var (token, _) in expiredTokens)
        {
            TokenStore.TryRemove(token, out _);
        }

        return Task.CompletedTask;
    }
}
