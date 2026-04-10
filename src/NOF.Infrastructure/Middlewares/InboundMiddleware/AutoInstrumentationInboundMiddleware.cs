using Microsoft.Extensions.Logging;
using NOF.Hosting;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NOF.Infrastructure;

/// <summary>Auto instrumentation step records execution metrics and logging.</summary>
/// <summary>
/// Auto-instrumentation middleware that automatically records handler execution logs, metrics, and performance data.
/// </summary>
public sealed class AutoInstrumentationInboundMiddleware : IInboundMiddleware, IAfter<TracingInboundMiddleware>
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
        var handlerType = context.Metadatas.TryGetValue("HandlerType", out var handlerTypeObj) && handlerTypeObj is Type type ? type : null;
        var messageName = context.Metadatas.TryGetValue("MessageName", out var mn) ? mn as string : context.Message?.GetType().FullName;
        var handlerName = context.Metadatas.TryGetValue("HandlerName", out var hn) ? hn as string : handlerType?.FullName;
        var tags = new KeyValuePair<string, object?>[]
        {
            new(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, handlerName),
            new(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, messageName)
        };
        _logger.LogDebug(
            "Executing handler {HandlerType} for message {MessageType}",
            handlerName, messageName);

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
                handlerType?.FullName, durationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            _errorCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);

            _logger.LogError(ex,
                "Handler {HandlerType} failed after {Duration}ms: {ErrorMessage}",
                handlerType?.FullName, durationMs, ex.Message);

            throw;
        }
    }
}
