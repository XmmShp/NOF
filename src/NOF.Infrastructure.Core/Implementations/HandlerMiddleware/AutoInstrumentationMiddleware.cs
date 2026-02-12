using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NOF.Infrastructure.Core;

/// <summary>Auto instrumentation step — records execution metrics and logging.</summary>
public class AutoInstrumentationMiddlewareStep : IHandlerMiddlewareStep<AutoInstrumentationMiddleware>, IAfter<ActivityTracingMiddlewareStep>;

/// <summary>
/// Auto-instrumentation middleware that automatically records handler execution logs, metrics, and performance data.
/// </summary>
public sealed class AutoInstrumentationMiddleware : IHandlerMiddleware
{
    private static readonly Counter<long> ExecutionCounter = HandlerPipelineTracing.Meter.CreateCounter<long>(
        HandlerPipelineTracing.Metrics.ExecutionCounter,
        description: HandlerPipelineTracing.MetricDescriptions.ExecutionCounter);
    private static readonly Histogram<double> ExecutionDuration = HandlerPipelineTracing.Meter.CreateHistogram<double>(
        HandlerPipelineTracing.Metrics.ExecutionDuration,
        unit: HandlerPipelineTracing.MetricUnits.Milliseconds,
        description: HandlerPipelineTracing.MetricDescriptions.ExecutionDuration);
    private static readonly Counter<long> ErrorCounter = HandlerPipelineTracing.Meter.CreateCounter<long>(
        HandlerPipelineTracing.Metrics.ErrorCounter,
        description: HandlerPipelineTracing.MetricDescriptions.ErrorCounter);

    private readonly ILogger<AutoInstrumentationMiddleware> _logger;

    public AutoInstrumentationMiddleware(ILogger<AutoInstrumentationMiddleware> logger)
    {
        _logger = logger;
    }

    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new(HandlerPipelineTracing.Tags.HandlerType, context.HandlerType),
            new(HandlerPipelineTracing.Tags.MessageType, context.MessageType)
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
