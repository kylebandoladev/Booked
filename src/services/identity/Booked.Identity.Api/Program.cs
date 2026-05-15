var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<Booked.Shared.Contracts.Security.JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<Booked.Shared.Contracts.Security.RateLimitSettings>(builder.Configuration.GetSection("RateLimit"));

// Register token service implementation from Infrastructure
builder.Services.AddSingleton<Booked.Shared.BuildingBlocks.Security.ITokenService, Booked.Identity.Infrastructure.Security.JwtTokenService>();
builder.Services.AddSingleton<Booked.Identity.Application.IRefreshTokenService, Booked.Identity.Infrastructure.Services.InMemoryRefreshTokenService>();

// Register rate limiting service
builder.Services.AddSingleton<Booked.Identity.Infrastructure.RateLimiting.IRateLimitService, Booked.Identity.Infrastructure.RateLimiting.InMemoryRateLimitService>();

builder.Services.AddControllers();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
	options.AddPolicy("frontend", policy =>
	{
		if (allowedOrigins.Length == 0)
		{
			policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
			return;
		}

		policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
	});
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseCors("frontend");

// Add rate limiting middleware (before authorization)
app.UseMiddleware<Booked.Identity.Infrastructure.RateLimiting.RateLimitingMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();

public sealed class AuthSettings
{
	public string AdminPassword { get; set; } = "admin123";
}
