using Booked.Identity.Application;
using Booked.Identity.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace Booked.Identity.Tests;

public class InMemoryRefreshTokenServiceTests
{
    private readonly IRefreshTokenService _refreshTokenService;

    public InMemoryRefreshTokenServiceTests()
    {
        _refreshTokenService = new InMemoryRefreshTokenService();
    }

    [Fact]
    public async Task CreateAsync_ShouldStoreRefreshToken()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token = "test_refresh_token_" + Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Act
        var result = await _refreshTokenService.CreateAsync(subject, token, expiresAt, "127.0.0.1", "Mozilla/5.0");

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().Be(token);
        result.Subject.Should().Be(subject);
        result.IsRevoked.Should().BeFalse();
        result.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnStoredToken()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token = "test_refresh_token_" + Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddDays(7);
        await _refreshTokenService.CreateAsync(subject, token, expiresAt, "127.0.0.1", "Mozilla/5.0");

        // Act
        var result = await _refreshTokenService.GetAsync(token);

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().Be(token);
        result.Subject.Should().Be(subject);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNullForNonexistentToken()
    {
        // Arrange
        var nonexistentToken = "nonexistent_" + Guid.NewGuid().ToString();

        // Act
        var result = await _refreshTokenService.GetAsync(nonexistentToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySubjectAsync_ShouldReturnValidTokenForSubject()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token1 = "token1_" + Guid.NewGuid().ToString();
        var token2 = "token2_" + Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        await _refreshTokenService.CreateAsync(subject, token1, expiresAt, "127.0.0.1", "Mozilla/5.0");
        await System.Threading.Tasks.Task.Delay(10);
        await _refreshTokenService.CreateAsync(subject, token2, expiresAt, "127.0.0.1", "Mozilla/5.0");

        // Act
        var result = await _refreshTokenService.GetBySubjectAsync(subject);

        // Assert
        result.Should().NotBeNull();
        new[] { token1, token2 }.Should().Contain(result!.Token); // Should return one of the valid tokens
        result.Subject.Should().Be(subject);
        result.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task GetBySubjectAsync_ShouldNotReturnRevokedToken()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token = "test_refresh_token_" + Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        await _refreshTokenService.CreateAsync(subject, token, expiresAt, "127.0.0.1", "Mozilla/5.0");
        await _refreshTokenService.RevokeAsync(token);

        // Act
        var result = await _refreshTokenService.GetBySubjectAsync(subject);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySubjectAsync_ShouldNotReturnExpiredToken()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token = "test_refresh_token_" + Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddSeconds(-1); // Already expired

        await _refreshTokenService.CreateAsync(subject, token, expiresAt, "127.0.0.1", "Mozilla/5.0");

        // Act
        var result = await _refreshTokenService.GetBySubjectAsync(subject);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RevokeAsync_ShouldMarkTokenAsRevoked()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token = "test_refresh_token_" + Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        await _refreshTokenService.CreateAsync(subject, token, expiresAt, "127.0.0.1", "Mozilla/5.0");

        // Act
        await _refreshTokenService.RevokeAsync(token);
        var result = await _refreshTokenService.GetAsync(token);

        // Assert
        result.Should().NotBeNull();
        result!.IsRevoked.Should().BeTrue();
        result.RevokedAt.Should().NotBeNull();
        result.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RevokeBySubjectAsync_ShouldRevokeAllTokensForSubject()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token1 = "token1_" + Guid.NewGuid().ToString();
        var token2 = "token2_" + Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        await _refreshTokenService.CreateAsync(subject, token1, expiresAt, "127.0.0.1", "Mozilla/5.0");
        await _refreshTokenService.CreateAsync(subject, token2, expiresAt, "127.0.0.1", "Mozilla/5.0");

        // Act
        await _refreshTokenService.RevokeBySubjectAsync(subject);

        var result1 = await _refreshTokenService.GetAsync(token1);
        var result2 = await _refreshTokenService.GetAsync(token2);

        // Assert
        result1.Should().NotBeNull();
        result1!.IsRevoked.Should().BeTrue();
        result2.Should().NotBeNull();
        result2!.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task CleanupExpiredAsync_ShouldRemoveExpiredTokens()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var expiredToken = "expired_" + Guid.NewGuid().ToString();
        var validToken = "valid_" + Guid.NewGuid().ToString();

        var expiredAt = DateTime.UtcNow.AddSeconds(-10);
        var validAt = DateTime.UtcNow.AddDays(7);

        await _refreshTokenService.CreateAsync(subject, expiredToken, expiredAt, "127.0.0.1", "Mozilla/5.0");
        await _refreshTokenService.CreateAsync(subject, validToken, validAt, "127.0.0.1", "Mozilla/5.0");

        // Act
        await _refreshTokenService.CleanupExpiredAsync();

        var resultExpired = await _refreshTokenService.GetAsync(expiredToken);
        var resultValid = await _refreshTokenService.GetAsync(validToken);

        // Assert
        resultExpired.Should().BeNull(); // Expired token should be cleaned up
        resultValid.Should().NotBeNull(); // Valid token should remain
    }

    [Fact]
    public async Task CreateAsync_ShouldStoreIpAddressAndUserAgent()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token = "test_refresh_token_" + Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var ipAddress = "192.168.1.100";
        var userAgent = "CustomClient/1.0";

        // Act
        var result = await _refreshTokenService.CreateAsync(subject, token, expiresAt, ipAddress, userAgent);

        // Assert
        result.IpAddress.Should().Be(ipAddress);
        result.UserAgent.Should().Be(userAgent);
    }

    [Fact]
    public async Task CreateAsync_ShouldSetCreatedAtToNow()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token = "test_refresh_token_" + Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var beforeCreate = DateTime.UtcNow;

        // Act
        var result = await _refreshTokenService.CreateAsync(subject, token, expiresAt, "127.0.0.1", "Mozilla/5.0");

        var afterCreate = DateTime.UtcNow;

        // Assert
        result.CreatedAt.Should().BeOnOrAfter(beforeCreate);
        result.CreatedAt.Should().BeOnOrBefore(afterCreate);
    }
}
