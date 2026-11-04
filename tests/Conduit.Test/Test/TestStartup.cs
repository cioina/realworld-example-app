using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Conduit.Test;

public class TestStartup(IConfiguration configuration, IWebHostEnvironment environment) : Startup(configuration, environment)
{
    public void ConfigureTestServices(IServiceCollection services)
    {
        ConfigureServices(services);
    }
}
