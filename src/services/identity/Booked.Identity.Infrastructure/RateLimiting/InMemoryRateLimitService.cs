using Booked.Shared.Contracts.Security;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Booked.Identity.Infrastructure.RateLimiting;

/// <summary>
/// In-memory rate limiting service using IP addresses as keys.
/// Tracks request counts and timestamps to enforce rate limits.
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Checks if a request should be allowed based on rate limit policy.
    /// </summary>
    /// <param name="clientId">Client IP address or identifier</param>
    /// <param name="policyName">Rate limit policy (e.g., "login", "register")</param>
    /// <returns>true if request is allowed, false if rate limit exceeded</returns>
    bool AllowRequest(string clientId, string policyName);

    /// <summary>
    /// Gets remaining requests for a client under a policy.
    /// </summary>
    int GetRemainingRequests(string clientId, string policyName);
}

public class InMemoryRateLimitService : IRateLimitService
{
    // Store: clientId -> policyName -> list of request timestamps
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<DateTime>>> _requestLog = new();
    private readonly IOptions<RateLimitSettings> _options;
    private readonly object _lockObj = new();

    // Policy configuration: policyName -> (maxRequests, windowMinutes)
    private readonly Dictionary<string, (int limit, int windowMinutes)> _policies;

    public InMemoryRateLimitService(IOptions<RateLimitSettings> options)
    {
        _options = options;
        _policies = new()
        {
            { "register", (options.Value.RegistrationLimit, options.Value.RegistrationLimitMinutes) },
            { "login", (options.Value.LoginLimit, options.Value.LoginLimitMinutes) },
            { "refresh", (options.Value.RefreshLimit, options.Value.RefreshLimitMinutes) }
        };
    }

    public bool AllowRequest(string clientId, string policyName)
    {
        if (!_options.Value.Enabled)
            return true;

        if (!_policies.TryGetValue(policyName, out var policy))
            return true; // Unknown policy, allow it

        lock (_lockObj)
        {
            var now = DateTime.UtcNow;
            var windowStart = now.AddMinutes(-policy.windowMinutes);

            // Get or create client log
            var clientLog = _requestLog.GetOrAdd(clientId, _ => new ConcurrentDictionary<string, List<DateTime>>());

            // Get or create policy log for this client
            var policyLog = clientLog.GetOrAdd(policyName, _ => new List<DateTime>());

            // Remove expired timestamps (older than window)
            policyLog.RemoveAll(ts => ts < windowStart);

            // Check if limit exceeded
            if (policyLog.Count >= policy.limit)
            {
                if (_options.Value.LogViolations)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[RateLimit] Policy '{policyName}' exceeded for client '{clientId}'. " +
                        $"Limit: {policy.limit} per {policy.windowMinutes} min");
                }

                return false;
            }

            // Allow and record this request
            policyLog.Add(now);
            return true;
        }
    }

    public int GetRemainingRequests(string clientId, string policyName)
    {
        if (!_policies.TryGetValue(policyName, out var policy))
            return policy.limit;

        lock (_lockObj)
        {
            var now = DateTime.UtcNow;
            var windowStart = now.AddMinutes(-policy.windowMinutes);

            if (_requestLog.TryGetValue(clientId, out var clientLog) &&
                clientLog.TryGetValue(policyName, out var policyLog))
            {
                var validRequests = policyLog.Count(ts => ts >= windowStart);
                return Math.Max(0, policy.limit - validRequests);
            }

            return policy.limit;
        }
    }
}
