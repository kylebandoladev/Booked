using Booked.Shared.Contracts.Auth;
using Booked.Shared.BuildingBlocks.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Booked.Identity.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private sealed class StoredCustomer
    {
        public required string Email { get; init; }
        public required string PasswordHash { get; init; }
        public required string FullName { get; init; }
    }

    private sealed class StoredOrganization
    {
        public required string Email { get; init; }
        public required string PasswordHash { get; init; }
        public required string OrganizationName { get; init; }
        public required string SubscriptionType { get; init; }
    }

    private static readonly ConcurrentDictionary<string, StoredCustomer> Users = new();
    private static readonly ConcurrentDictionary<string, StoredOrganization> Organizations = new();
    private static readonly PasswordHasher<StoredCustomer> CustomerPasswordHasher = new();
    private static readonly PasswordHasher<StoredOrganization> OrganizationPasswordHasher = new();

    private readonly string _adminPassword;
    private readonly ITokenService _tokenService;

    public AuthController(IOptions<AuthSettings> authOptions, ITokenService tokenService)
    {
        _adminPassword = string.IsNullOrWhiteSpace(authOptions.Value.AdminPassword)
            ? "admin123"
            : authOptions.Value.AdminPassword;

        _tokenService = tokenService;
    }

    [HttpGet("health")]
    public ActionResult<object> Health()
    {
        return Ok(new { status = "ok", service = "identity" });
    }

    [HttpPost("customer/register")]
    public ActionResult<AuthResponse> CustomerRegister([FromBody] CustomerRegisterRequest req)
    {
        var normalizedEmail = NormalizeEmail(req.Email);
        var normalizedName = req.FullName.Trim();
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToArray();
            return BadRequest(new AuthResponse { Success = false, Message = string.Join("; ", errors) });
        }

        if (Users.Values.Any(u => u.Email == normalizedEmail))
        {
            return BadRequest(new AuthResponse { Success = false, Message = "Email already exists" });
        }

        var userId = Guid.NewGuid().ToString();
        var user = new StoredCustomer
        {
            Email = normalizedEmail,
            PasswordHash = string.Empty,
            FullName = normalizedName
        };

        var hash = CustomerPasswordHasher.HashPassword(user, req.Password);
        var storedUser = new StoredCustomer
        {
            Email = normalizedEmail,
            PasswordHash = hash,
            FullName = normalizedName
        };

        Users.TryAdd(userId, storedUser);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Customer registered successfully",
            User = new UserInfo { Id = userId, Email = normalizedEmail, Role = "Customer", Name = normalizedName }
        });
    }

    [HttpPost("customer/login")]
    public ActionResult<AuthResponse> CustomerLogin([FromBody] CustomerLoginRequest req)
    {
        var normalizedEmail = NormalizeEmail(req.Email);

        var user = Users.FirstOrDefault(u => u.Value.Email == normalizedEmail);
        if (string.IsNullOrWhiteSpace(user.Key))
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid credentials" });
        }

        var verify = CustomerPasswordHasher.VerifyHashedPassword(user.Value, user.Value.PasswordHash, req.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid credentials" });
        }

        var token = _tokenService.GenerateAccessToken(user.Key);
        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Login successful",
            Token = token,
            User = new UserInfo
            {
                Id = user.Key,
                Email = user.Value.Email,
                Role = "Customer",
                Name = user.Value.FullName
            }
        });
    }

    [HttpPost("organization/register")]
    public ActionResult<AuthResponse> OrganizationRegister([FromBody] OrganizationRegisterRequest req)
    {
        var normalizedEmail = NormalizeEmail(req.Email);
        var normalizedOrgName = req.OrganizationName.Trim();
        var subscriptionType = req.SubscriptionType.Trim().ToLowerInvariant();
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToArray();
            return BadRequest(new AuthResponse { Success = false, Message = string.Join("; ", errors) });
        }

        if (subscriptionType is not ("monthly" or "quarterly" or "yearly"))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "SubscriptionType must be monthly, quarterly, or yearly"
            });
        }

        if (Organizations.Values.Any(o => o.Email == normalizedEmail))
        {
            return BadRequest(new AuthResponse { Success = false, Message = "Email already exists" });
        }

        var orgId = Guid.NewGuid().ToString();
        var org = new StoredOrganization
        {
            Email = normalizedEmail,
            PasswordHash = string.Empty,
            OrganizationName = normalizedOrgName,
            SubscriptionType = subscriptionType
        };

        var hash = OrganizationPasswordHasher.HashPassword(org, req.Password);
        var storedOrg = new StoredOrganization
        {
            Email = normalizedEmail,
            PasswordHash = hash,
            OrganizationName = normalizedOrgName,
            SubscriptionType = subscriptionType
        };

        Organizations.TryAdd(orgId, storedOrg);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Organization registered successfully",
            User = new UserInfo
            {
                Id = orgId,
                Email = normalizedEmail,
                Role = "Organization",
                OrganizationName = normalizedOrgName
            }
        });
    }

    [HttpPost("organization/login")]
    public ActionResult<AuthResponse> OrganizationLogin([FromBody] OrganizationLoginRequest req)
    {
        var normalizedEmail = NormalizeEmail(req.Email);

        var org = Organizations.FirstOrDefault(o => o.Value.Email == normalizedEmail);
        if (string.IsNullOrWhiteSpace(org.Key))
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid credentials" });
        }

        var verify = OrganizationPasswordHasher.VerifyHashedPassword(org.Value, org.Value.PasswordHash, req.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid credentials" });
        }

        var token = _tokenService.GenerateAccessToken(org.Key);
        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Login successful",
            Token = token,
            User = new UserInfo
            {
                Id = org.Key,
                Email = org.Value.Email,
                Role = "Organization",
                OrganizationName = org.Value.OrganizationName
            }
        });
    }

    [HttpPost("admin/login")]
    public ActionResult<AuthResponse> AdminLogin([FromBody] AdminLoginRequest req)
    {
        if (req.Password != _adminPassword)
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Invalid admin credentials" });
        }

        var adminId = "admin-" + req.AdminKey.Trim();
        var token = _tokenService.GenerateAccessToken(adminId);

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Admin login successful",
            Token = token,
            User = new UserInfo { Id = adminId, Email = "admin@booked.app", Role = "Admin" }
        });
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    
}