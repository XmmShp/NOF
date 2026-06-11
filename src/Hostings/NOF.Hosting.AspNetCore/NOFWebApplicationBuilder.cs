using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using NOF.Abstraction;
using NOF.Infrastructure;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

[assembly: InternalsVisibleTo("NOF.Integration.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]

namespace NOF.Hosting.AspNetCore;

public class NOFWebApplicationBuilder : NOFAppBuilder<WebApplication>
{
    public WebApplicationBuilder WebApplicationBuilder { get; }

    protected NOFWebApplicationBuilder(string[] args, Assembly? applicationAssembly)
    {
        WebApplicationBuilder = WebApplication.CreateBuilder(args);
        if (applicationAssembly is not null)
        {
            this.AddApplicationPart(applicationAssembly);
        }
    }

    public static NOFWebApplicationBuilder Create(string[] args)
    {
        var builder = new NOFWebApplicationBuilder(args, Assembly.GetCallingAssembly());
        builder.AddInfrastructureDefaults();
        builder.AddRegistrationStep(new AspNetCoreRegistrationStep());
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            var nof = JsonSerializerOptions.NOF;
            options.SerializerOptions.PropertyNameCaseInsensitive = nof.PropertyNameCaseInsensitive;
            options.SerializerOptions.DefaultIgnoreCondition = nof.DefaultIgnoreCondition;
            options.SerializerOptions.ReferenceHandler = nof.ReferenceHandler;
            options.SerializerOptions.PropertyNamingPolicy = nof.PropertyNamingPolicy;

            foreach (var converter in nof.Converters)
            {
                options.SerializerOptions.Converters.Add(converter);
            }

            options.SerializerOptions.TypeInfoResolver = nof.TypeInfoResolver;
        });
        builder.Services.AddOptions<CorsSettingsOptions>();
        builder.Services.AddCors();
        builder.Services.AddInitializationStep(new CorsInitializationStep());
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer(static (document, _, _) =>
            {
                const string scheme = NOFAbstractionConstants.Transport.Headers.Authorization;

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[scheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = scheme
                };

                document.Security ??= [];
                var requirement = new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(scheme, document, string.Empty)] = []
                };
                document.Security.Add(requirement);

                return Task.CompletedTask;
            });
        });
        return builder;
    }

    protected override Task<WebApplication> BuildApplicationAsync()
    {
        return Task.FromResult(WebApplicationBuilder.Build());
    }

    public override void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
    {
        ((IHostApplicationBuilder)WebApplicationBuilder).ConfigureContainer(factory, configure);
    }

    public override IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();

    public override IConfigurationManager Configuration => WebApplicationBuilder.Configuration;

    public override IHostEnvironment Environment => WebApplicationBuilder.Environment;

    public override ILoggingBuilder Logging => WebApplicationBuilder.Logging;

    public override IMetricsBuilder Metrics => WebApplicationBuilder.Metrics;

    public override IServiceCollection Services => WebApplicationBuilder.Services;
}
