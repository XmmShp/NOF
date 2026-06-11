using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using NOF.Infrastructure;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace NOF.Hosting.AspNetCore;

/// <summary>
/// Registers ASP.NET Core-specific services including health checks and
/// ASP.NET Core OpenTelemetry instrumentation, and maps health check endpoints.
/// </summary>
public class AspNetCoreRegistrationStep : IServiceRegistrationStep
{
    public TopologyComparison Compare(IServiceRegistrationStep other) => TopologyComparison.DoesNotMatter;

    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    private const string Tag = "live";

    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), [Tag]);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<HttpRequestInboundAdapter>();
        builder.Services.AddOptions<HttpHeaderOutboundOptions>();

        builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddAspNetCoreInstrumentation());
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
            tracing.AddAspNetCoreInstrumentation(options =>
                options.Filter = context =>
                    !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                    && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
            ));

        builder.Services.AddInitializationStep(new RpcHttpEndpointResultWrappingInitializationStep());
        builder.Services.AddInitializationStep(new HealthCheckInitializationStep());

        return ValueTask.CompletedTask;
    }

    private class HealthCheckInitializationStep : IApplicationInitializationStep
    {
        public TopologyComparison Compare(IApplicationInitializationStep other)
            => TopologyComparison.DoesNotMatter;

        public Task ExecuteAsync(IHost app)
        {
            if (app is IEndpointRouteBuilder rt)
            {
                rt.MapHealthChecks(HealthEndpointPath);
                rt.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
                {
                    Predicate = r => r.Tags.Contains(Tag)
                });
            }

            return Task.CompletedTask;
        }
    }

    private class RpcHttpEndpointResultWrappingInitializationStep : IApplicationInitializationStep
    {
        public TopologyComparison Compare(IApplicationInitializationStep other)
            => TopologyComparison.DoesNotMatter;

        public Task ExecuteAsync(IHost app)
        {
            if (app is IApplicationBuilder actualApp)
            {
                actualApp.UseMiddleware<DaemonServiceResolutionMiddleware>();
            }

            return Task.CompletedTask;
        }
    }
}
