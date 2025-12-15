using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace NOF;

public static partial class __NOF_Integration_Observability_AspNetCore__
{
    extension(INOFObservableAppBuilder builder)
    {
        public INOFObservableAppBuilder AddAspNetCoreTelemetry<THostApplication>(INOFAppBuilder<THostApplication> aspAppBuilder)
            where THostApplication : class, IHost, IEndpointRouteBuilder
        {
            builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
            aspAppBuilder.AddApplicationConfig(new ObservabilityConfig<THostApplication>());

            builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddAspNetCoreInstrumentation());
            builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
                    tracing.AddAspNetCoreInstrumentation(options =>
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                            ));
            return builder;
        }
    }
}
