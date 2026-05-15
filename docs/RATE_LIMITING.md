# Rate Limiting Implementation

## Overview

Rate limiting has been implemented on the Booked Identity API to protect against brute-force attacks and Denial-of-Service (DoS) vectors. The implementation uses an in-memory, IP-based tracking system that is simple, performant, and suitable for single-instance deployments.

## Architecture

### Components

1. **RateLimitSettings** (`Booked.Shared.Contracts/Security/RateLimitSettings.cs`)
   - Configuration class for rate limiting policies
   - Bindable from `appsettings.json` via `IOptions<RateLimitSettings>`

2. **IRateLimitService / InMemoryRateLimitService** (`Booked.Identity.Infrastructure/RateLimiting/`)
   - Core service tracking request counts per IP per policy
   - Uses `ConcurrentDictionary` for thread-safe storage
   - Automatic cleanup of expired timestamps

3. **RateLimitingMiddleware** (`Booked.Identity.Infrastructure/RateLimiting/`)
   - HTTP middleware that enforces rate limits
   - Runs before authorization to catch abusive traffic early
   - Returns 429 (Too Many Requests) when limit exceeded

### Rate Limiting Policies

| Policy | Limit | Window | Endpoints |
|--------|-------|--------|-----------|
| `register` | 5 requests | 15 minutes | `/auth/customer/register`, `/auth/organization/register` |
| `login` | 5 requests | 15 minutes | `/auth/customer/login`, `/auth/organization/login`, `/auth/admin/login` |
| `refresh` | 10 requests | 1 minute | `/auth/refresh` |

### Configuration

**Development** (`appsettings.Development.json`):
```json
{
  "RateLimit": {
    "Enabled": true,
    "RegistrationLimit": 5,
    "RegistrationLimitMinutes": 15,
    "LoginLimit": 5,
    "LoginLimitMinutes": 15,
    "RefreshLimit": 10,
    "RefreshLimitMinutes": 1,
    "HttpStatusCode": 429,
    "LogViolations": true
  }
}
```

**Production** (`appsettings.Production.json`):
- Same defaults as development (tunable via environment variables if needed)

## Implementation Details

### Request Flow

```
HTTP Request
    ↓
RateLimitingMiddleware
    ↓
Extract Client IP (X-Forwarded-For or RemoteIpAddress)
    ↓
Determine Policy (register/login/refresh)
    ↓
Check with IRateLimitService
    ├─ Within Limit → Allow + Continue
    └─ Exceeded → Return 429 + Block
```

### Response Headers

When rate limiting is active, the following headers are added to all responses:

- `X-RateLimit-Limit`: Maximum requests allowed in window
- `X-RateLimit-Remaining`: Remaining requests for this IP
- `X-RateLimit-Reset`: Unix timestamp of window reset

### 429 Response Example

```json
{
  "success": false,
  "message": "Rate limit exceeded. Too many requests.",
  "retryAfter": 60
}
```

## Security Considerations

### What's Protected

✅ Brute-force attack prevention (login/register)
✅ DoS amplification prevention (refresh endpoint)
✅ Replay attack deterrence (limits token refresh velocity)
✅ Per-IP tracking (distributed attack detection)

### Limitations

⚠️ **Single-instance only**: In-memory storage doesn't work across multiple API instances
⚠️ **No persistence**: Rate limit counters reset on restart
⚠️ **IP spoofing**: Relies on X-Forwarded-For header (ensure proxies validate)
⚠️ **Memory growth**: Long-running instances may accumulate stale entries

### For Production / Scaling

For multi-instance deployments, consider:
1. **Redis backend**: Use `StackExchange.Redis` for distributed tracking
2. **Azure Cache for Redis**: Managed Redis service on Azure
3. **Third-party rate limiting**: Cloudflare, AWS WAF, etc.

Example migration to Redis (future enhancement):
```csharp
// Add to Program.cs
builder.Services.AddStackExchangeRedisCache(opts => opts.Configuration = "redis:6379");
builder.Services.AddSingleton<IRateLimitService, DistributedRateLimitService>();
```

## Testing

### Unit Tests

9 comprehensive unit tests in `tests/unit/Booked.Identity.Tests/RateLimitServiceTests.cs`:

```
AllowRequest_WithinLimit_ShouldAllow                   ✓
AllowRequest_ExceedingLimit_ShouldDeny                 ✓
AllowRequest_DifferentClients_ShouldTrackSeparately    ✓
AllowRequest_DifferentPolicies_ShouldTrackSeparately   ✓
GetRemainingRequests_ShouldReturnCorrectCount          ✓
AllowRequest_DisabledRateLimit_ShouldAlwaysAllow       ✓
AllowRequest_UnknownPolicy_ShouldAllow                 ✓
GetRemainingRequests_NoRequests_ShouldReturnFullLimit  ✓
GetRemainingRequests_ExceededLimit_ShouldReturnZero    ✓
```

### Integration Tests

All 13 existing integration tests pass with rate limiting middleware active:
```
✓ Customer register/login/refresh/logout flows
✓ Organization register/login/refresh flows
✓ Admin login flow
✓ Token rotation validation
✓ Revocation verification
```

### Manual Testing

Test rate limiting with curl:

```bash
# 1st request - allowed
curl -X POST http://localhost:5154/api/auth/customer/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password"}'

# Show remaining (should be 4)
curl -i -X POST http://localhost:5154/api/auth/customer/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password"}' \
  | grep X-RateLimit-Remaining

# After 5 attempts from same IP - should get 429
# Response:
# HTTP/1.1 429 Too Many Requests
# {"success":false,"message":"Rate limit exceeded. Too many requests.","retryAfter":60}
```

## Monitoring & Diagnostics

### Debug Logging

When `LogViolations: true` in config:
```
[RateLimit] Policy 'login' exceeded for client '192.168.1.1'. Limit: 5 per 15 min
```

### Metrics to Track

1. **429 responses per endpoint**: Indicates attack patterns
2. **Repeated violators**: IPs that consistently hit limits
3. **False positives**: Legitimate users (e.g., aggressive auto-refresh)

### Future Enhancements

- [ ] Metrics export (Prometheus/OpenTelemetry)
- [ ] Dashboard showing top violating IPs
- [ ] Configurable policies per endpoint
- [ ] Whitelist/bypass for internal services
- [ ] Graduated backoff (longer delays after repeated violations)

## Deployment Notes

### GitHub Actions

Rate limiting is automatically tested in CI:
- All unit tests pass (33/33)
- All integration tests pass (13/13)
- Build succeeds in Release mode

### Local Development

Rate limiting is **enabled by default** in Development config. To disable for testing:

```json
{
  "RateLimit": {
    "Enabled": false
  }
}
```

### Performance Impact

- **Memory**: ~100 bytes per tracked IP per policy per time window
- **CPU**: O(1) lookup, O(n) cleanup (where n = requests in window)
- **Latency**: <1ms additional per request

## Configuration Reference

```csharp
public class RateLimitSettings
{
    // Enable/disable entire system (default: true)
    public bool Enabled { get; set; } = true;

    // Registration endpoint limits
    public int RegistrationLimit { get; set; } = 5;           // requests
    public int RegistrationLimitMinutes { get; set; } = 15;   // time window

    // Login endpoint limits
    public int LoginLimit { get; set; } = 5;
    public int LoginLimitMinutes { get; set; } = 15;

    // Token refresh limits (allow more frequently as legitimate need)
    public int RefreshLimit { get; set; } = 10;
    public int RefreshLimitMinutes { get; set; } = 1;

    // HTTP status code (RFC 6585)
    public int HttpStatusCode { get; set; } = 429;

    // Verbose logging of violations
    public bool LogViolations { get; set; } = true;
}
```

## Troubleshooting

### Q: Legitimate users getting 429 errors

**A:** Check if they're behind a shared proxy. All traffic from that proxy appears as one IP:
1. Verify `X-Forwarded-For` header is configured correctly in your reverse proxy
2. Increase `LoginLimit` or `RegistrationLimitMinutes` in config
3. Implement granular tracking (userId + IP instead of just IP)

### Q: Rate limit not working

**A:** Verify middleware is registered in `Program.cs`:
```csharp
app.UseMiddleware<RateLimitingMiddleware>();
```

And ensure `RateLimit` section exists in `appsettings.json`.

### Q: Want to reset limits on restart

**A:** Current implementation uses volatile in-memory storage, so restarting clears all counters. For persistent limits, migrate to Redis-backed implementation.

## Future Roadmap

- [ ] Redis/distributed support
- [ ] Per-user rate limiting (not just IP)
- [ ] Adaptive limits (increase for known users)
- [ ] Graceful degradation (fallback to lenient limits if rate limit service fails)
- [ ] Analytics dashboard integration

