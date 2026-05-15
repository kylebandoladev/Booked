using Microsoft.AspNetCore.Mvc.Testing;
using Booked.Identity.Api;

namespace Booked.Identity.Integration.Tests;

public class AuthApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Override any services here if needed for testing
            // For now, we use the default configuration
        });

        builder.UseEnvironment("Development");
    }
}
