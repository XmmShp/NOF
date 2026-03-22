using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NOF.Infrastructure;

/// <summary>Auto instrumentation step records execution metrics and logging.</summary>
public class AutoInstrumentationInboundMiddlewareStep : IInboundMiddlewareStep<AutoInstrumentationInboundMiddlewareStep, AutoInstrumentationInboundMiddleware>, IAfter<TracingInboundMiddlewareStep>;

/// <summary>
/// Auto-instrumentation middleware that automatically records handler execution logs, metrics, and performance data.
/// </summary>
public sealed class AutoInstrumentationInboundMiddleware : IInboundMiddleware
{
    private static readonly Counter<long> _executionCounter = NOFInfrastructureConstants.InboundPipeline.Meter.CreateCounter<long>(
        NOFInfrastructureConstants.InboundPipeline.Metrics.ExecutionCounter,
        description: NOFInfrastructureConstants.InboundPipeline.MetricDescriptions.ExecutionCounter);
    private static readonly Histogram<double> _executionDuration = NOFInfrastructureConstants.InboundPipeline.Meter.CreateHistogram<double>(
        NOFInfrastructureConstants.InboundPipeline.Metrics.ExecutionDuration,
        unit: NOFInfrastructureConstants.InboundPipeline.MetricUnits.Milliseconds,
        description: NOFInfrastructureConstants.InboundPipeline.MetricDescriptions.ExecutionDuration);
    private static readonly Counter<long> _errorCounter = NOFInfrastructureConstants.InboundPipeline.Meter.CreateCounter<long>(
        NOFInfrastructureConstants.InboundPipeline.Metrics.ErrorCounter,
        description: NOFInfrastructureConstants.InboundPipeline.MetricDescriptions.ErrorCounter);

    private readonly ILogger<AutoInstrumentationInboundMiddleware> _logger;

    public AutoInstrumentationInboundMiddleware(ILogger<AutoInstrumentationInboundMiddleware> logger)
    {
        _logger = logger;
    }

    public async ValueTask InvokeAsync(InboundContext context, InboundDelegate next, CancellationToken cancellationToken)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerType),
            new(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, context.MessageType)
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

            _executionCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);

            _logger.LogDebug(
                "Handler {HandlerType} completed successfully in {Duration}ms",
                context.HandlerType, durationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            _errorCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);

            _logger.LogError(ex,
                "Handler {HandlerType} failed after {Duration}ms: {ErrorMessage}",
                context.HandlerType, durationMs, ex.Message);

            throw;
        }
    }
}
