using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using NOF.Abstraction;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

[assembly: InternalsVisibleTo("NOF.Integration.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]

namespace NOF.Hosting.AspNetCore;

public class NOFWebApplicationBuilder : IHostApplicationBuilder
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
        builder.AddNOFInfrastructure();
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
        builder.Services.AddHealthChecks().AddCheck("self", static () => HealthCheckResult.Healthy(), ["live"]);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<HttpRequestInboundAdapter>();
        builder.Services.AddOptions<HttpHeaderOutboundOptions>();
        builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddAspNetCoreInstrumentation());
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
            tracing.AddAspNetCoreInstrumentation(options =>
                options.Filter = context =>
                    !context.Request.Path.StartsWithSegments("/health")
                    && !context.Request.Path.StartsWithSegments("/alive")));
        builder.Services.AddInitializationStep(new DaemonServiceResolutionInitializationStep());
        builder.Services.TryAddSingleton<HttpEndpointMappingState>();
        builder.Services.TryAddInitializationStep<RpcServerHttpEndpointInitializationStep>();
        builder.Services.AddInitializationStep(new HealthCheckInitializationStep());
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

    public Task<WebApplication> BuildAsync()
        => this.BuildNOFAsync(WebApplicationBuilder.Build);

    public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull
    {
        ((IHostApplicationBuilder)WebApplicationBuilder).ConfigureContainer(factory, configure);
    }

    public IDictionary<object, object> Properties => ((IHostApplicationBuilder)WebApplicationBuilder).Properties;

    public IConfigurationManager Configuration => WebApplicationBuilder.Configuration;

    public IHostEnvironment Environment => WebApplicationBuilder.Environment;

    public ILoggingBuilder Logging => WebApplicationBuilder.Logging;

    public IMetricsBuilder Metrics => WebApplicationBuilder.Metrics;

    public IServiceCollection Services => WebApplicationBuilder.Services;

    private sealed class HealthCheckInitializationStep : IApplicationInitializationStep
    {
        public TopologyComparison Compare(IApplicationInitializationStep other)
            => TopologyComparison.DoesNotMatter;

        public Task ExecuteAsync(IHost app)
        {
            if (app is IEndpointRouteBuilder routeBuilder)
            {
                routeBuilder.MapHealthChecks("/health");
                routeBuilder.MapHealthChecks("/alive", new HealthCheckOptions
                {
                    Predicate = static registration => registration.Tags.Contains("live")
                });
            }

            return Task.CompletedTask;
        }
    }
}
