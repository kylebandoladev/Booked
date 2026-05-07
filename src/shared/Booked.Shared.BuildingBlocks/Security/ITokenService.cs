using Booked.Shared.Contracts.Auth;
using Booked.Shared.Contracts.Security;

namespace Booked.Shared.BuildingBlocks.Security;

public interface ITokenService
{
    AuthToken GenerateAccessToken(string subject, IEnumerable<string>? roles = null);

    AuthToken GenerateRefreshToken(string subject);

    bool ValidateAccessToken(string token, out string? subject);
}
