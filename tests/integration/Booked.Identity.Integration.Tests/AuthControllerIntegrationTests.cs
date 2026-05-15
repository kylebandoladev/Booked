using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Booked.Shared.Contracts.Auth;
using FluentAssertions;
using Xunit;

namespace Booked.Identity.Integration.Tests;

public class AuthControllerIntegrationTests : IAsyncLifetime
{
    private readonly AuthApiFactory _factory;
    private HttpClient _client = null!;

    public AuthControllerIntegrationTests()
    {
        _factory = new AuthApiFactory();
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        
        // Verify API is healthy
        var response = await _client.GetAsync("/api/auth/health");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task CustomerRegister_WithValidData_ShouldSucceed()
    {
        // Arrange
        var email = $"test+{Guid.NewGuid().ToString("N").Substring(0, 8)}@booked.test";
        var request = new CustomerRegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd123!",
            FullName = "Test User"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/customer/register", content);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AuthResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.User.Should().NotBeNull();
        result.User!.Email.Should().Be(email.ToLowerInvariant());
        result.Token.Should().BeNull(); // No token on registration
    }

    [Fact]
    public async Task CustomerRegister_WithDuplicateEmail_ShouldFail()
    {
        // Arrange
        var email = $"test+{Guid.NewGuid().ToString("N").Substring(0, 8)}@booked.test";
        var request = new CustomerRegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd123!",
            FullName = "Test User"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act - Register first time
        var response1 = await _client.PostAsync("/api/auth/customer/register", content);
        response1.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // Act - Register again with same email
        var response2 = await _client.PostAsync("/api/auth/customer/register", content);

        // Assert
        response2.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        var responseBody = await response2.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AuthResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result!.Success.Should().BeFalse();
        result.Message.Should().Contain("Email already exists");
    }

    [Fact]
    public async Task CustomerRegister_WithInvalidEmail_ShouldFail()
    {
        // Arrange
        var request = new CustomerRegisterRequest
        {
            Email = "not-an-email",
            Password = "P@ssw0rd123!",
            FullName = "Test User"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/customer/register", content);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CustomerRegister_WithShortPassword_ShouldFail()
    {
        // Arrange
        var request = new CustomerRegisterRequest
        {
            Email = $"test+{Guid.NewGuid().ToString("N").Substring(0, 8)}@booked.test",
            Password = "short",
            FullName = "Test User"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/customer/register", content);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FullAuthFlow_RegisterLoginRefreshLogout_ShouldSucceed()
    {
        // Arrange
        var email = $"test+{Guid.NewGuid().ToString("N").Substring(0, 8)}@booked.test";
        var password = "P@ssw0rd123!";

        // Act 1: Register
        var registerRequest = new CustomerRegisterRequest
        {
            Email = email,
            Password = password,
            FullName = "Test User"
        };

        var registerContent = new StringContent(
            JsonSerializer.Serialize(registerRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        var registerResponse = await _client.PostAsync("/api/auth/customer/register", registerContent);
        registerResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var registerBody = await registerResponse.Content.ReadAsStringAsync();
        var registerResult = JsonSerializer.Deserialize<AuthResponse>(registerBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var userId = registerResult!.User!.Id;

        // Act 2: Login
        var loginRequest = new CustomerLoginRequest
        {
            Email = email,
            Password = password
        };

        var loginContent = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        var loginResponse = await _client.PostAsync("/api/auth/customer/login", loginContent);
        loginResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        var loginResult = JsonSerializer.Deserialize<AuthResponse>(loginBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var firstAccessToken = loginResult!.Token!.AccessToken;
        var firstRefreshToken = loginResult!.Token!.RefreshToken;

        // Verify tokens are valid JWTs
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(firstAccessToken);
        jwtToken.Subject.Should().Be(userId);

        // Act 3: Refresh
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = firstRefreshToken
        };

        var refreshContent = new StringContent(
            JsonSerializer.Serialize(refreshRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        var refreshResponse = await _client.PostAsync("/api/auth/refresh", refreshContent);
        refreshResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var refreshBody = await refreshResponse.Content.ReadAsStringAsync();
        var refreshResult = JsonSerializer.Deserialize<AuthResponse>(refreshBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var secondAccessToken = refreshResult!.Token!.AccessToken;
        var secondRefreshToken = refreshResult!.Token!.RefreshToken;

        // Verify new tokens are different
        secondAccessToken.Should().NotBe(firstAccessToken);
        secondRefreshToken.Should().NotBe(firstRefreshToken);

        // Act 4: Logout
        var logoutRequest = new LogoutRequest
        {
            RefreshToken = secondRefreshToken
        };

        var logoutContent = new StringContent(
            JsonSerializer.Serialize(logoutRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        var logoutResponse = await _client.PostAsync("/api/auth/logout", logoutContent);
        logoutResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // Act 5: Try to use revoked refresh token - should fail
        var revokedRefreshRequest = new RefreshTokenRequest
        {
            RefreshToken = secondRefreshToken
        };

        var revokedRefreshContent = new StringContent(
            JsonSerializer.Serialize(revokedRefreshRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        var revokedRefreshResponse = await _client.PostAsync("/api/auth/refresh", revokedRefreshContent);

        // Assert
        revokedRefreshResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldFail()
    {
        // Arrange
        var email = $"test+{Guid.NewGuid().ToString("N").Substring(0, 8)}@booked.test";

        // Register first
        var registerRequest = new CustomerRegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd123!",
            FullName = "Test User"
        };

        var registerContent = new StringContent(
            JsonSerializer.Serialize(registerRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        await _client.PostAsync("/api/auth/customer/register", registerContent);

        // Act - Try to login with wrong password
        var loginRequest = new CustomerLoginRequest
        {
            Email = email,
            Password = "WrongPassword123!"
        };

        var loginContent = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        var loginResponse = await _client.PostAsync("/api/auth/customer/login", loginContent);

        // Assert
        loginResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ShouldFail()
    {
        // Arrange
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = "invalid_refresh_token_" + Guid.NewGuid().ToString()
        };

        var refreshContent = new StringContent(
            JsonSerializer.Serialize(refreshRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var refreshResponse = await _client.PostAsync("/api/auth/refresh", refreshContent);

        // Assert
        refreshResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        var body = await refreshResponse.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AuthResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        result!.Message.Should().Contain("Invalid");
    }

    [Fact]
    public async Task Refresh_TwiceWithSameToken_SecondShouldFail()
    {
        // Arrange
        var email = $"test+{Guid.NewGuid().ToString("N").Substring(0, 8)}@booked.test";
        var password = "P@ssw0rd123!";

        // Register and login
        var registerRequest = new CustomerRegisterRequest
        {
            Email = email,
            Password = password,
            FullName = "Test User"
        };

        var registerContent = new StringContent(
            JsonSerializer.Serialize(registerRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        await _client.PostAsync("/api/auth/customer/register", registerContent);

        var loginRequest = new CustomerLoginRequest
        {
            Email = email,
            Password = password
        };

        var loginContent = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        var loginResponse = await _client.PostAsync("/api/auth/customer/login", loginContent);
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        var loginResult = JsonSerializer.Deserialize<AuthResponse>(loginBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var refreshToken = loginResult!.Token!.RefreshToken;

        // Act 1: First refresh - should succeed
        var refreshRequest1 = new RefreshTokenRequest
        {
            RefreshToken = refreshToken
        };

        var refreshContent1 = new StringContent(
            JsonSerializer.Serialize(refreshRequest1),
            System.Text.Encoding.UTF8,
            "application/json");

        var refreshResponse1 = await _client.PostAsync("/api/auth/refresh", refreshContent1);
        refreshResponse1.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // Act 2: Try to use original refresh token again - should fail (token was rotated)
        var refreshRequest2 = new RefreshTokenRequest
        {
            RefreshToken = refreshToken
        };

        var refreshContent2 = new StringContent(
            JsonSerializer.Serialize(refreshRequest2),
            System.Text.Encoding.UTF8,
            "application/json");

        var refreshResponse2 = await _client.PostAsync("/api/auth/refresh", refreshContent2);

        // Assert
        refreshResponse2.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OrganizationRegister_WithValidData_ShouldSucceed()
    {
        // Arrange
        var email = $"org+{Guid.NewGuid().ToString("N").Substring(0, 8)}@booked.test";
        var request = new OrganizationRegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd123!",
            OrganizationName = "Test Org",
            SubscriptionType = "yearly"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/organization/register", content);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AuthResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.User!.Role.Should().Be("Organization");
        result.User.OrganizationName.Should().Be("Test Org");
    }

    [Fact]
    public async Task OrganizationRegister_WithInvalidSubscriptionType_ShouldFail()
    {
        // Arrange
        var email = $"org+{Guid.NewGuid().ToString("N").Substring(0, 8)}@booked.test";
        var request = new OrganizationRegisterRequest
        {
            Email = email,
            Password = "P@ssw0rd123!",
            OrganizationName = "Test Org",
            SubscriptionType = "invalid"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/organization/register", content);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AuthResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result!.Success.Should().BeFalse();
        result.Message.Should().Contain("SubscriptionType");
    }

    [Fact]
    public async Task AdminLogin_WithValidPassword_ShouldSucceed()
    {
        // Arrange
        var request = new AdminLoginRequest
        {
            AdminKey = "admin",
            Password = "admin123"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/admin/login", content);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AuthResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Token.Should().NotBeNull();
        result.User!.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task AdminLogin_WithInvalidPassword_ShouldFail()
    {
        // Arrange
        var request = new AdminLoginRequest
        {
            AdminKey = "admin",
            Password = "wrongpassword"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/auth/admin/login", content);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Revoke_ShouldRevokeAllTokensForSubject()
    {
        // Arrange
        var email = $"test+{Guid.NewGuid().ToString("N").Substring(0, 8)}@booked.test";
        var password = "P@ssw0rd123!";

        // Register and login
        var registerRequest = new CustomerRegisterRequest
        {
            Email = email,
            Password = password,
            FullName = "Test User"
        };

        var registerContent = new StringContent(
            JsonSerializer.Serialize(registerRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        await _client.PostAsync("/api/auth/customer/register", registerContent);

        var loginRequest = new CustomerLoginRequest
        {
            Email = email,
            Password = password
        };

        var loginContent = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        var loginResponse = await _client.PostAsync("/api/auth/customer/login", loginContent);
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        var loginResult = JsonSerializer.Deserialize<AuthResponse>(loginBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var refreshToken = loginResult!.Token!.RefreshToken;

        // Act: Revoke all tokens
        var revokeRequest = new LogoutRequest
        {
            RefreshToken = refreshToken
        };

        var revokeContent = new StringContent(
            JsonSerializer.Serialize(revokeRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        var revokeResponse = await _client.PostAsync("/api/auth/revoke", revokeContent);
        revokeResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // Assert: Try to refresh with the revoked token - should fail
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = refreshToken
        };

        var refreshContent = new StringContent(
            JsonSerializer.Serialize(refreshRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        var refreshResponse = await _client.PostAsync("/api/auth/refresh", refreshContent);
        refreshResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}
