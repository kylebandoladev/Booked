using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Booked.Identity.Infrastructure.Security;
using Booked.Shared.Contracts.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Booked.Identity.Tests;

public class JwtTokenServiceTests
{
    private readonly JwtSettings _jwtSettings;
    private readonly JwtTokenService _tokenService;

    public JwtTokenServiceTests()
    {
        _jwtSettings = new JwtSettings
        {
            Secret = "this_is_a_super_secret_key_that_is_long_enough_for_hmac_sha256_operations",
            Issuer = "booked",
            Audience = "booked_clients",
            AccessTokenExpirationMinutes = 60,
            RefreshTokenExpirationDays = 7
        };

        var options = Options.Create(_jwtSettings);
        _tokenService = new JwtTokenService(options);
    }

    [Fact]
    public void GenerateAccessToken_ShouldReturnValidAuthToken()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var roles = new[] { "Customer" };

        // Act
        var result = _tokenService.GenerateAccessToken(subject, roles);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void GenerateAccessToken_ShouldCreateJwtWithCorrectClaims()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var roles = new[] { "Customer", "Premium" };

        // Act
        var result = _tokenService.GenerateAccessToken(subject, roles);
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);

        // Assert
        token.Subject.Should().Be(subject);
        token.Issuer.Should().Be(_jwtSettings.Issuer);
        token.Audiences.Should().Contain(_jwtSettings.Audience);
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == subject);
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        token.ValidFrom.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
        token.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void GenerateAccessToken_ShouldHaveCorrectExpiration()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var beforeGeneration = DateTime.UtcNow;

        // Act
        var result = _tokenService.GenerateAccessToken(subject);
        var afterGeneration = DateTime.UtcNow;

        // Assert
        var expectedMinExpiry = beforeGeneration.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);
        var expectedMaxExpiry = afterGeneration.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

        result.ExpiresAt.Should().BeOnOrAfter(expectedMinExpiry);
        result.ExpiresAt.Should().BeOnOrBefore(expectedMaxExpiry.AddSeconds(1));
    }

    [Fact]
    public void GenerateAccessToken_ShouldCreateUniqueRefreshTokens()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();

        // Act
        var result1 = _tokenService.GenerateAccessToken(subject);
        var result2 = _tokenService.GenerateAccessToken(subject);

        // Assert
        result1.RefreshToken.Should().NotBe(result2.RefreshToken);
    }

    [Fact]
    public void GenerateAccessToken_RefreshTokenShouldContainSubject()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();

        // Act
        var result = _tokenService.GenerateAccessToken(subject);

        // Assert
        result.RefreshToken.Should().Contain(subject);
    }

    [Fact]
    public void ValidateAccessToken_ShouldAcceptValidToken()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token = _tokenService.GenerateAccessToken(subject);

        // Act
        var isValid = _tokenService.ValidateAccessToken(token.AccessToken, out var extractedSubject);

        // Assert
        isValid.Should().BeTrue();
        extractedSubject.Should().Be(subject);
    }

    [Fact]
    public void ValidateAccessToken_ShouldRejectMalformedToken()
    {
        // Arrange
        var malformedToken = "not.a.valid.jwt.token";

        // Act
        var isValid = _tokenService.ValidateAccessToken(malformedToken, out var subject);

        // Assert
        isValid.Should().BeFalse();
        subject.Should().BeNull();
    }

    [Fact]
    public void ValidateAccessToken_ShouldRejectTokenWithWrongSignature()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token = _tokenService.GenerateAccessToken(subject);

        // Tamper with the signature
        var parts = token.AccessToken.Split('.');
        var tampered = string.Join(".", parts[0], parts[1], "invalidsignature");

        // Act
        var isValid = _tokenService.ValidateAccessToken(tampered, out _);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateAccessToken_ShouldRejectExpiredToken()
    {
        // Arrange - create a token with very short expiry
        var shortLivedSettings = new JwtSettings
        {
            Secret = _jwtSettings.Secret,
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            AccessTokenExpirationMinutes = -1, // Already expired
            RefreshTokenExpirationDays = 7
        };

        var options = Options.Create(shortLivedSettings);
        var shortLivedService = new JwtTokenService(options);

        var subject = Guid.NewGuid().ToString();
        var expiredToken = shortLivedService.GenerateAccessToken(subject);

        // Wait a moment to ensure expiration
        System.Threading.Thread.Sleep(100);

        // Act
        var isValid = _tokenService.ValidateAccessToken(expiredToken.AccessToken, out _);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateAccessToken_ShouldRejectTokenWithDifferentSecret()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();
        var token = _tokenService.GenerateAccessToken(subject);

        // Create a validator with a different secret
        var wrongSettings = new JwtSettings
        {
            Secret = "this_is_a_different_secret_key_that_is_long_enough_for_hmac_sha256",
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            AccessTokenExpirationMinutes = 60,
            RefreshTokenExpirationDays = 7
        };

        var options = Options.Create(wrongSettings);
        var wrongSecretService = new JwtTokenService(options);

        // Act
        var isValid = wrongSecretService.ValidateAccessToken(token.AccessToken, out _);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void GenerateAccessToken_WithoutRoles_ShouldSucceed()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();

        // Act
        var result = _tokenService.GenerateAccessToken(subject);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateAccessToken_ShouldUseConfiguredSettings()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString();

        // Act
        var result = _tokenService.GenerateAccessToken(subject);
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);

        // Assert
        token.Issuer.Should().Be(_jwtSettings.Issuer);
        token.Audiences.Should().Contain(_jwtSettings.Audience);
    }

    [Fact]
    public void GenerateAccessToken_ShouldHaveUniqueJti()
    {
        // Arrange & Act
        var subject = Guid.NewGuid().ToString();
        var token1 = _tokenService.GenerateAccessToken(subject);
        var token2 = _tokenService.GenerateAccessToken(subject);

        var handler = new JwtSecurityTokenHandler();
        var jwt1 = handler.ReadJwtToken(token1.AccessToken);
        var jwt2 = handler.ReadJwtToken(token2.AccessToken);

        var jti1 = jwt1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = jwt2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        // Assert
        jti1.Should().NotBe(jti2);
    }
}
