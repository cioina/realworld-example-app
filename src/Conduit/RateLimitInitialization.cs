using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Conduit;

public static class RateLimitInitialization
{
    public static IApplicationBuilder InitializeRateLimit(this IApplicationBuilder app)
    {
        using (var scope = app.ApplicationServices.CreateScope())
        {
            // get the IpPolicyStore instance
            var ipPolicyStore = scope.ServiceProvider.GetRequiredService<IIpPolicyStore>();

            // seed IP data from appsettings
            ipPolicyStore.SeedAsync();
        }

        return app;
    }
}
