using Booked.Shared.Contracts.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Booked.Identity.Infrastructure.RateLimiting;

/// <summary>
/// Middleware that enforces rate limits based on client IP and policy.
/// Responds with 429 Too Many Requests when limit is exceeded.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitService _rateLimitService;
    private readonly IOptions<RateLimitSettings> _options;

    public RateLimitingMiddleware(RequestDelegate next, IRateLimitService rateLimitService, IOptions<RateLimitSettings> options)
    {
        _next = next;
        _rateLimitService = rateLimitService;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Value.Enabled)
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIp(context);
        var policy = GetPolicyForEndpoint(context);

        if (!string.IsNullOrEmpty(policy))
        {
            var allowed = _rateLimitService.AllowRequest(clientIp, policy);
            var remaining = _rateLimitService.GetRemainingRequests(clientIp, policy);

            // Add rate limit headers (similar to HTTP RateLimit standard)
            context.Response.Headers.Add("X-RateLimit-Limit", _options.Value.RefreshLimit.ToString());
            context.Response.Headers.Add("X-RateLimit-Remaining", remaining.ToString());
            context.Response.Headers.Add("X-RateLimit-Reset", GetResetTime().ToString());

            if (!allowed)
            {
                context.Response.StatusCode = _options.Value.HttpStatusCode;
                context.Response.ContentType = "application/json";
                
                var response = new { success = false, message = "Rate limit exceeded. Too many requests.", retryAfter = 60 };
                var json = JsonSerializer.Serialize(response);
                await context.Response.WriteAsync(json);
                return;
            }
        }

        await _next(context);
    }

    private string GetClientIp(HttpContext context)
    {
        // Try X-Forwarded-For first (for proxied requests)
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
        {
            var ips = forwarded.ToString().Split(',');
            return ips[0].Trim();
        }

        // Fall back to RemoteIpAddress
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private string? GetPolicyForEndpoint(HttpContext context)
    {
        var path = context.Request.Path.ToString().ToLower();
        var method = context.Request.Method;

        if (method != "POST")
            return null;

        // Determine policy based on endpoint
        if (path.Contains("/auth/customer/register") || path.Contains("/auth/organization/register"))
            return "register";

        if (path.Contains("/auth/customer/login") || path.Contains("/auth/organization/login") || path.Contains("/auth/admin/login"))
            return "login";

        if (path.Contains("/auth/refresh"))
            return "refresh";

        return null;
    }

    private long GetResetTime()
    {
        // Return Unix timestamp when rate limit window resets (in 60 seconds from now)
        var resetTime = DateTimeOffset.UtcNow.AddSeconds(60);
        return resetTime.ToUnixTimeSeconds();
    }
}
