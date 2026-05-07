using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Booked.Shared.BuildingBlocks.Security;
using Booked.Shared.Contracts.Auth;
using Booked.Shared.Contracts.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Booked.Identity.Infrastructure.Security;

public class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public AuthToken GenerateAccessToken(string subject, IEnumerable<string>? roles = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, subject),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (roles != null)
        {
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        }

        var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        var handler = new JwtSecurityTokenHandler();
        var accessToken = handler.WriteToken(token);

        return new AuthToken
        {
            AccessToken = accessToken,
            RefreshToken = GenerateRefreshTokenString(subject),
            ExpiresAt = expires
        };
    }

    public AuthToken GenerateRefreshToken(string subject)
    {
        return new AuthToken
        {
            AccessToken = string.Empty,
            RefreshToken = GenerateRefreshTokenString(subject),
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays)
        };
    }

    public bool ValidateAccessToken(string token, out string? subject)
    {
        subject = null;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_settings.Secret);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = !string.IsNullOrEmpty(_settings.Issuer),
            ValidIssuer = _settings.Issuer,
            ValidateAudience = !string.IsNullOrEmpty(_settings.Audience),
            ValidAudience = _settings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateRefreshTokenString(string subject)
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + "_" + subject;
    }
}
