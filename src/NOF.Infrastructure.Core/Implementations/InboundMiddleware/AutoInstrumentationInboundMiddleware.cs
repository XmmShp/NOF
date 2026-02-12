using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NOF.Infrastructure.Core;

/// <summary>Auto instrumentation step — records execution metrics and logging.</summary>
public class AutoInstrumentationInboundMiddlewareStep : IInboundMiddlewareStep<AutoInstrumentationInboundMiddleware>, IAfter<TracingInboundMiddlewareStep>;

/// <summary>
/// Auto-instrumentation middleware that automatically records handler execution logs, metrics, and performance data.
/// </summary>
public sealed class AutoInstrumentationInboundMiddleware : IInboundMiddleware
{
    private static readonly Counter<long> ExecutionCounter = NOFInfrastructureCoreConstants.InboundPipeline.Meter.CreateCounter<long>(
        NOFInfrastructureCoreConstants.InboundPipeline.Metrics.ExecutionCounter,
        description: NOFInfrastructureCoreConstants.InboundPipeline.MetricDescriptions.ExecutionCounter);
    private static readonly Histogram<double> ExecutionDuration = NOFInfrastructureCoreConstants.InboundPipeline.Meter.CreateHistogram<double>(
        NOFInfrastructureCoreConstants.InboundPipeline.Metrics.ExecutionDuration,
        unit: NOFInfrastructureCoreConstants.InboundPipeline.MetricUnits.Milliseconds,
        description: NOFInfrastructureCoreConstants.InboundPipeline.MetricDescriptions.ExecutionDuration);
    private static readonly Counter<long> ErrorCounter = NOFInfrastructureCoreConstants.InboundPipeline.Meter.CreateCounter<long>(
        NOFInfrastructureCoreConstants.InboundPipeline.Metrics.ErrorCounter,
        description: NOFInfrastructureCoreConstants.InboundPipeline.MetricDescriptions.ErrorCounter);

    private readonly ILogger<AutoInstrumentationInboundMiddleware> _logger;

    public AutoInstrumentationInboundMiddleware(ILogger<AutoInstrumentationInboundMiddleware> logger)
    {
        _logger = logger;
    }

    public async ValueTask InvokeAsync(InboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new(NOFInfrastructureCoreConstants.InboundPipeline.Tags.HandlerType, context.HandlerType),
            new(NOFInfrastructureCoreConstants.InboundPipeline.Tags.MessageType, context.MessageType)
        };

        _logger.LogDebug(
            "Executing handler {HandlerType} for message {MessageType}",
            context.HandlerType, context.MessageType);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(cancellationToken);

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            ExecutionCounter.Add(1, tags);
            ExecutionDuration.Record(durationMs, tags);

            _logger.LogDebug(
                "Handler {HandlerType} completed successfully in {Duration}ms",
                context.HandlerType, durationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            ErrorCounter.Add(1, tags);
            ExecutionDuration.Record(durationMs, tags);

            _logger.LogError(ex,
                "Handler {HandlerType} failed after {Duration}ms: {ErrorMessage}",
                context.HandlerType, durationMs, ex.Message);

            throw;
        }
    }
}
