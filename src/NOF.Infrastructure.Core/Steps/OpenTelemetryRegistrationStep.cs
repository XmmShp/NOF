using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NOF.Infrastructure.Abstraction;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Configures OpenTelemetry logging, metrics, and tracing infrastructure.
/// Conditionally enables the OTLP exporter when the OTEL_EXPORTER_OTLP_ENDPOINT environment variable is set.
/// </summary>
public class OpenTelemetryRegistrationStep : IBaseSettingsServiceRegistrationStep
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(NOFInfrastructureCoreConstants.InboundPipeline.MeterName);
                metrics.AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(NOFInfrastructureCoreConstants.InboundPipeline.ActivitySourceName);
                tracing.AddSource(NOFInfrastructureCoreConstants.Messaging.ActivitySourceName);
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddHttpClientInstrumentation();
            });

        const string otelExporterOtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration[otelExporterOtlpEndpoint]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return ValueTask.CompletedTask;
    }
}
