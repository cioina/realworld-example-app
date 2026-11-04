using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AspNetCoreRateLimit;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

#if DEBUG
using NSwag.Generation.Processors.Contexts;
using NSwag.Generation.Processors;
#else
#endif

namespace Conduit;

#if DEBUG
public class ControllerFilterProcessor : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        if (context.ControllerType != null)
        {
            Console.WriteLine("[NSWAG] CONTROLLER " + context.ControllerType.Name + " " + context.MethodInfo.Name);

            // Optional: temporarily skip controllers you know or suspect to have issues
            switch (context.ControllerType.Name)
            {
                case "1ArticlesController":
                    //case "IdentityController":
                    return false;
                default:
                    break;
            }

            return true;
        }
        else
        {
            Console.WriteLine("[NSWAG] ENDPOINT " + context.AllOperationDescriptions.First().Path + " " + context.MethodInfo.DeclaringType + " " + context.MethodInfo.Name);
            return true;
        }
    }
}
#else
#endif

#region Startup
public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
{
    public IConfiguration Configuration { get; } = configuration;
    public IWebHostEnvironment Environment { get; } = environment;

    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
        services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));

        services
            .AddMemoryCache()
            .AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>()
            .AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>()
            .AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>()
            .AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>()
            .AddScoped<IpRateLimitMiddleware>()
            .AddScoped<ClientRateLimitMiddleware>();

        services.AddConduitDatabase(Configuration);

        services.AddLocalization(x => x.ResourcesPath = "Resources");

        // Inject an implementation of ISwaggerProvider with defaulted settings applied
        services.AddSwaggerGen(x =>
        {
            x.AddSecurityDefinition(
                "Bearer",
                new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Please insert JWT with Bearer into field",
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    BearerFormat = "JWT",
                }
            );

            x.SupportNonNullableReferenceTypes();

            x.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>(),
            });
            x.SwaggerDoc("v1", new OpenApiInfo { Title = "RealWorld API", Version = "v1" });
            x.CustomSchemaIds(y => y.FullName);
            x.DocInclusionPredicate((_, _) => true);
            x.TagActionsBy(y => new List<string> { y.GroupName ?? throw new InvalidOperationException() });
            x.CustomSchemaIds(s => s.FullName?.Replace("+", "."));
        });

        //services.AddCors();
        services.AddControllers(opt =>
            {
                opt.Conventions.Add(new GroupByApiRootConvention());
                // the RealWorld API spec mounts all endpoints under /api
                opt.Conventions.Add(
                    new ApiRoutePrefixConvention(Configuration["ApiPrefix"] ?? "api")
                );
                opt.Filters.Add<ValidatorActionFilter>();
            })
            // the RealWorld spec expects nullable fields (bio, image, ...) to be serialized as explicit nulls
            .AddJsonOptions(opt =>
                opt.JsonSerializerOptions.DefaultIgnoreCondition = System
                    .Text
                    .Json
                    .Serialization
                    .JsonIgnoreCondition
                    .Never
            );

        services.AddConduit();

        services.AddJwt(Configuration);
        services.AddAuthorization();

        //.AddDomain()
        //.AddApplication(this.Configuration)
        //.AddInfrastructure(this.Configuration, this.Environment)
        //.AddWebComponents();

#if DEBUG
        services
        .AddOpenApiDocument((configure, sp) =>
        {
            configure.Title = "BlogAngular API";
            configure.OperationProcessors.Insert(0, new ControllerFilterProcessor());
        });
#else
#endif
    }

    #region Configure
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {

        app.UseMiddleware<ErrorHandlingMiddleware>();

        //// Enable middleware to serve generated Swagger as a JSON endpoint
        //app.UseSwagger(c => c.RouteTemplate = "swagger/{documentName}/swagger.json");

        //// Enable middleware to serve swagger-ui assets(HTML, JS, CSS etc.)
        //app.UseSwaggerUI(x => x.SwaggerEndpoint("/swagger/v1/swagger.json", "RealWorld API V1"));

        //if (Configuration.GetValue("Database:ApplyMigrations", true))
        //{
        //    using var scope = app.Services.CreateScope();
        //    var dbContext = scope.ServiceProvider.GetRequiredService<ConduitContext>();
        //    await DatabaseInitializer.InitializeAsync(dbContext);
        //}

        //app.UseMiddleware<ValidationExceptionHandlerMiddleware>();

        //if (env.EnvironmentName == "Test" || env.EnvironmentName == "Development")
        //{
        //    app.UseStaticFiles();
        //}
        //else
        //{
        //    app.UseStaticFiles(new StaticFileOptions
        //    {
        //        FileProvider = new PhysicalFileProvider(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot"))
        //    });
        //}

        app
        .UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor |
               ForwardedHeaders.XForwardedProto
        })
        .UseRouting()
        .UseCors(options => options
                .WithHeaders(
                    "Content-Type",
                    "X-Auth-Token",
                    "Origin",
                    "Authorization")
                .WithMethods(
                    "POST",
                    "GET",
                    "OPTIONS",
                    "PUT",
                    "PATCH",
                    "DELETE")
                .WithExposedHeaders(
                    "Cache-Control",
                    "Content-Language",
                    "Content-Type",
                    "Expires",
                    "Last-Modified",
                    "Pragma")
                .AllowAnyOrigin()
           )
        .InitializeRateLimit()
        .UseMiddleware<IpRateLimitMiddleware>()
        .UseAuthentication()
        .UseAuthorization()
        .UseMiddleware<ClientRateLimitMiddleware>()
        .UseEndpoints(endpoints =>
          {
              endpoints.MapDefaultControllerRoute();
          });
        //.Use(async (context, next) =>
        //  {
        //      await next();

        //      if (context.Response.StatusCode == 404 && !context.Response.HasStarted)
        //      {
        //          var cacheMaxAge = 0;// 86400;// 1 day
        //          // Set Last-Modified header
        //          var lastModifiedDate = DateTime.UtcNow;
        //          context.Response.Headers.LastModified = lastModifiedDate.ToString("R");

        //          // Set cache control headers
        //          context.Response.Headers.CacheControl = $"public, max-age={cacheMaxAge}";
        //          context.Response.Headers.Expires = DateTime.UtcNow.AddSeconds(cacheMaxAge).ToString("R");

        //          await context.Response.WriteAsync(GetIndexTemplate());
        //      }
        //  })
        //.InitializeDb();
    }
    #endregion Configure

    //private static string GetIndexTemplate()
    //{
    //    var assembly = Assembly.GetExecutingAssembly();
    //    string[] names = assembly.GetManifestResourceNames();
    //    var baseTemplateStream = assembly.GetManifestResourceStream(names[0]);
    //    using var baseReader = new StreamReader(baseTemplateStream!, Encoding.UTF8);
    //    var baseTemplate = baseReader.ReadToEnd();

    //    return baseTemplate;
    //}
}
    #endregion Startup
