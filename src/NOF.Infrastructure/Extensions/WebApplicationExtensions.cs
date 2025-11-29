using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Collections.Concurrent;

namespace NOF;

public static class WebApplicationExtensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddServiceDefaults()
        {
            builder.ConfigureOpenTelemetry();

            builder.AddDefaultHealthChecks();

            builder.Services.AddServiceDiscovery();

            builder.Services.ConfigureHttpClientDefaults(http =>
            {
                http.AddStandardResilienceHandler();
                http.AddServiceDiscovery();
            });

            return builder;
        }

        public WebApplicationBuilder ConfigureOpenTelemetry()
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });

            builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation();
                })
                .WithTracing(tracing =>
                {
                    tracing.AddSource(builder.Environment.ApplicationName)
                        .AddAspNetCoreInstrumentation(tracing =>
                            // Exclude health check requests from tracing
                            tracing.Filter = context =>
                                !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                                && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                        )
                        .AddHttpClientInstrumentation();
                });

            builder.AddOpenTelemetryExporters();

            return builder;
        }

        private WebApplicationBuilder AddOpenTelemetryExporters()
        {
            var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

            if (useOtlpExporter)
            {
                builder.Services.AddOpenTelemetry().UseOtlpExporter();
            }

            return builder;
        }

        public WebApplicationBuilder AddDefaultHealthChecks()
        {
            builder.Services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

            return builder;
        }
    }

    extension(WebApplication app)
    {
        public WebApplication MapDefaultEndpoints()
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });

            return app;
        }

        public WebApplication MapHttpEndpoints(params HttpEndpoint[] endpoints)
        {
            foreach (var endpoint in endpoints)
            {
                var routeHandler = endpoint.Method switch
                {
                    HttpVerb.Get => app.MapGet(endpoint.Route, CreateHandler(endpoint.RequestType, true)),
                    HttpVerb.Post => app.MapPost(endpoint.Route, CreateHandler(endpoint.RequestType, false)),
                    HttpVerb.Put => app.MapPut(endpoint.Route, CreateHandler(endpoint.RequestType, false)),
                    HttpVerb.Delete => app.MapDelete(endpoint.Route, CreateHandler(endpoint.RequestType, false)),
                    HttpVerb.Patch => app.MapPatch(endpoint.Route, CreateHandler(endpoint.RequestType, false)),
                    _ => throw new NotSupportedException($"Unsupported verb: {endpoint.Method}")
                };

                if (endpoint.AllowAnonymous)
                {
                    routeHandler.AllowAnonymous();
                }

                if (!string.IsNullOrEmpty(endpoint.Permission))
                {
                    routeHandler.RequirePermission(endpoint.Permission);
                }
            }

            return app;
        }
    }

    private static readonly ConcurrentDictionary<(Type, bool), Delegate> HandlerCache = new();

    private static Delegate CreateHandler(Type requestType, bool isQuery)
    {
        return HandlerCache.GetOrAdd((requestType, isQuery), static key =>
        {
            var (rt, iq) = key;
            return HandlerFactory.Create(rt, iq);
        });
    }
}
