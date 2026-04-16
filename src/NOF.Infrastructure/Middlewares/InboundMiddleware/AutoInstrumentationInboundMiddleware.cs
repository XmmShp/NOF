using Microsoft.Extensions.Logging;
using NOF.Abstraction;
using NOF.Hosting;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NOF.Infrastructure;

public sealed class AutoInstrumentationInboundMiddleware :
    ICommandInboundMiddleware,
    INotificationInboundMiddleware,
    IRequestInboundMiddleware,
    IAfter<TracingInboundMiddleware>
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

    public async ValueTask InvokeAsync(CommandInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var messageName = context.Message.GetType().DisplayName;
        var tags = new KeyValuePair<string, object?>[]
        {
            new(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerType.DisplayName),
            new(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, messageName)
        };
        _logger.LogDebug("Executing handler {HandlerType} for message {MessageType}", context.HandlerType.DisplayName, messageName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(cancellationToken);

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            _executionCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);
            _logger.LogDebug("Handler {HandlerType} completed successfully in {Duration}ms", context.HandlerType.DisplayName, durationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            _errorCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);
            _logger.LogError(ex, "Handler {HandlerType} failed after {Duration}ms: {ErrorMessage}", context.HandlerType.DisplayName, durationMs, ex.Message);
            throw;
        }
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var messageName = context.Message.GetType().DisplayName;
        var tags = new KeyValuePair<string, object?>[]
        {
            new(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerType.DisplayName),
            new(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, messageName)
        };
        _logger.LogDebug("Executing handler {HandlerType} for message {MessageType}", context.HandlerType.DisplayName, messageName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(cancellationToken);

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            _executionCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);
            _logger.LogDebug("Handler {HandlerType} completed successfully in {Duration}ms", context.HandlerType.DisplayName, durationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            _errorCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);
            _logger.LogError(ex, "Handler {HandlerType} failed after {Duration}ms: {ErrorMessage}", context.HandlerType.DisplayName, durationMs, ex.Message);
            throw;
        }
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var messageName = $"{context.ServiceType.DisplayName}.{context.MethodName}";
        var tags = new KeyValuePair<string, object?>[]
        {
            new(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerType.DisplayName),
            new(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, messageName)
        };
        _logger.LogDebug("Executing handler {HandlerType} for message {MessageType}", context.HandlerType.DisplayName, messageName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(cancellationToken);

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            _executionCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);
            _logger.LogDebug("Handler {HandlerType} completed successfully in {Duration}ms", context.HandlerType.DisplayName, durationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            _errorCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);
            _logger.LogError(ex, "Handler {HandlerType} failed after {Duration}ms: {ErrorMessage}", context.HandlerType.DisplayName, durationMs, ex.Message);
            throw;
        }
    }
}
