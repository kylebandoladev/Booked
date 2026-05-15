using Booked.Identity.Infrastructure.RateLimiting;
using Booked.Shared.Contracts.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Booked.Identity.Tests;

public class InMemoryRateLimitServiceTests
{
    private readonly IOptions<RateLimitSettings> _options;
    private readonly IRateLimitService _rateLimitService;

    public InMemoryRateLimitServiceTests()
    {
        var settings = new RateLimitSettings
        {
            Enabled = true,
            RegistrationLimit = 3,
            RegistrationLimitMinutes = 1,
            LoginLimit = 2,
            LoginLimitMinutes = 1,
            RefreshLimit = 5,
            RefreshLimitMinutes = 1
        };

        _options = Options.Create(settings);
        _rateLimitService = new InMemoryRateLimitService(_options);
    }

    [Fact]
    public void AllowRequest_WithinLimit_ShouldAllow()
    {
        // Arrange
        var clientId = "192.168.1.1";
        var policy = "register";

        // Act
        var result1 = _rateLimitService.AllowRequest(clientId, policy);
        var result2 = _rateLimitService.AllowRequest(clientId, policy);
        var result3 = _rateLimitService.AllowRequest(clientId, policy);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeTrue();
    }

    [Fact]
    public void AllowRequest_ExceedingLimit_ShouldDeny()
    {
        // Arrange
        var clientId = "192.168.1.1";
        var policy = "register"; // Limit: 3 per 1 minute

        // Act - Make 4 requests (1 more than limit)
        _rateLimitService.AllowRequest(clientId, policy);
        _rateLimitService.AllowRequest(clientId, policy);
        _rateLimitService.AllowRequest(clientId, policy);
        var result4 = _rateLimitService.AllowRequest(clientId, policy);

        // Assert
        result4.Should().BeFalse();
    }

    [Fact]
    public void AllowRequest_DifferentClients_ShouldTrackSeparately()
    {
        // Arrange
        var client1 = "192.168.1.1";
        var client2 = "192.168.1.2";
        var policy = "login"; // Limit: 2 per 1 minute

        // Act - Client 1 hits limit
        _rateLimitService.AllowRequest(client1, policy);
        _rateLimitService.AllowRequest(client1, policy);
        var client1_deny = _rateLimitService.AllowRequest(client1, policy);

        // Client 2 should still have quota
        var client2_allow1 = _rateLimitService.AllowRequest(client2, policy);
        var client2_allow2 = _rateLimitService.AllowRequest(client2, policy);

        // Assert
        client1_deny.Should().BeFalse();
        client2_allow1.Should().BeTrue();
        client2_allow2.Should().BeTrue();
    }

    [Fact]
    public void AllowRequest_DifferentPolicies_ShouldTrackSeparately()
    {
        // Arrange
        var clientId = "192.168.1.1";
        var registerPolicy = "register"; // Limit: 3
        var loginPolicy = "login";       // Limit: 2

        // Act - Max out register policy (3 requests)
        _rateLimitService.AllowRequest(clientId, registerPolicy);
        _rateLimitService.AllowRequest(clientId, registerPolicy);
        _rateLimitService.AllowRequest(clientId, registerPolicy);

        // Login should still have quota (separate counter)
        var login1 = _rateLimitService.AllowRequest(clientId, loginPolicy);
        var login2 = _rateLimitService.AllowRequest(clientId, loginPolicy);
        var login3 = _rateLimitService.AllowRequest(clientId, loginPolicy);

        // Assert
        login1.Should().BeTrue();
        login2.Should().BeTrue();
        login3.Should().BeFalse(); // Limit is 2 for login
    }

    [Fact]
    public void GetRemainingRequests_ShouldReturnCorrectCount()
    {
        // Arrange
        var clientId = "192.168.1.1";
        var policy = "refresh"; // Limit: 5

        // Act
        _rateLimitService.AllowRequest(clientId, policy);
        _rateLimitService.AllowRequest(clientId, policy);
        var remaining = _rateLimitService.GetRemainingRequests(clientId, policy);

        // Assert
        remaining.Should().Be(3);
    }

    [Fact]
    public void AllowRequest_DisabledRateLimit_ShouldAlwaysAllow()
    {
        // Arrange
        var settings = new RateLimitSettings { Enabled = false, RegistrationLimit = 1 };
        var options = Options.Create(settings);
        var service = new InMemoryRateLimitService(options);

        // Act - Try to exceed limit
        var result1 = service.AllowRequest("192.168.1.1", "register");
        var result2 = service.AllowRequest("192.168.1.1", "register");
        var result3 = service.AllowRequest("192.168.1.1", "register");

        // Assert - All should be allowed when disabled
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeTrue();
    }

    [Fact]
    public void AllowRequest_UnknownPolicy_ShouldAllow()
    {
        // Arrange
        var clientId = "192.168.1.1";
        var unknownPolicy = "unknown_policy";

        // Act
        var result = _rateLimitService.AllowRequest(clientId, unknownPolicy);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetRemainingRequests_NoRequests_ShouldReturnFullLimit()
    {
        // Arrange
        var clientId = "192.168.1.1";
        var policy = "refresh"; // Limit: 5

        // Act
        var remaining = _rateLimitService.GetRemainingRequests(clientId, policy);

        // Assert
        remaining.Should().Be(5);
    }

    [Fact]
    public void GetRemainingRequests_ExceededLimit_ShouldReturnZero()
    {
        // Arrange
        var clientId = "192.168.1.1";
        var policy = "register"; // Limit: 3

        // Act - Make 3 requests (at limit)
        _rateLimitService.AllowRequest(clientId, policy);
        _rateLimitService.AllowRequest(clientId, policy);
        _rateLimitService.AllowRequest(clientId, policy);
        var remaining = _rateLimitService.GetRemainingRequests(clientId, policy);

        // Assert
        remaining.Should().Be(0);
    }
}
