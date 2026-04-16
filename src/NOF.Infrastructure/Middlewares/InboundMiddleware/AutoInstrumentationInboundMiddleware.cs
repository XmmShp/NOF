using Microsoft.Extensions.Logging;
using NOF.Hosting;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NOF.Infrastructure;

public sealed class CommandAutoInstrumentationInboundMiddleware : ICommandInboundMiddleware, IAfter<CommandTracingInboundMiddleware>
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

    private readonly ILogger<CommandAutoInstrumentationInboundMiddleware> _logger;

    public CommandAutoInstrumentationInboundMiddleware(ILogger<CommandAutoInstrumentationInboundMiddleware> logger)
    {
        _logger = logger;
    }

    public async ValueTask InvokeAsync(CommandInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var messageName = context.MessageType.FullName ?? context.MessageType.Name;
        var tags = new KeyValuePair<string, object?>[]
        {
            new(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerName),
            new(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, messageName)
        };
        _logger.LogDebug("Executing handler {HandlerType} for message {MessageType}", context.HandlerName, messageName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(cancellationToken);

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            _executionCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);
            _logger.LogDebug("Handler {HandlerType} completed successfully in {Duration}ms", context.HandlerName, durationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            _errorCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);
            _logger.LogError(ex, "Handler {HandlerType} failed after {Duration}ms: {ErrorMessage}", context.HandlerName, durationMs, ex.Message);
            throw;
        }
    }
}

public sealed class NotificationAutoInstrumentationInboundMiddleware : INotificationInboundMiddleware, IAfter<NotificationTracingInboundMiddleware>
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

    private readonly ILogger<NotificationAutoInstrumentationInboundMiddleware> _logger;

    public NotificationAutoInstrumentationInboundMiddleware(ILogger<NotificationAutoInstrumentationInboundMiddleware> logger)
    {
        _logger = logger;
    }

    public async ValueTask InvokeAsync(NotificationInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var messageName = context.MessageType.FullName ?? context.MessageType.Name;
        var tags = new KeyValuePair<string, object?>[]
        {
            new(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerName),
            new(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, messageName)
        };
        _logger.LogDebug("Executing handler {HandlerType} for message {MessageType}", context.HandlerName, messageName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(cancellationToken);

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            _executionCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);
            _logger.LogDebug("Handler {HandlerType} completed successfully in {Duration}ms", context.HandlerName, durationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            _errorCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);
            _logger.LogError(ex, "Handler {HandlerType} failed after {Duration}ms: {ErrorMessage}", context.HandlerName, durationMs, ex.Message);
            throw;
        }
    }
}

public sealed class RequestAutoInstrumentationInboundMiddleware : IRequestInboundMiddleware, IAfter<RequestTracingInboundMiddleware>
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

    private readonly ILogger<RequestAutoInstrumentationInboundMiddleware> _logger;

    public RequestAutoInstrumentationInboundMiddleware(ILogger<RequestAutoInstrumentationInboundMiddleware> logger)
    {
        _logger = logger;
    }

    public async ValueTask InvokeAsync(RequestInboundContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        var messageName = $"{context.ServiceType.FullName ?? context.ServiceType.Name}.{context.MethodName}";
        var tags = new KeyValuePair<string, object?>[]
        {
            new(NOFInfrastructureConstants.InboundPipeline.Tags.HandlerType, context.HandlerName),
            new(NOFInfrastructureConstants.InboundPipeline.Tags.MessageType, messageName)
        };
        _logger.LogDebug("Executing handler {HandlerType} for message {MessageType}", context.HandlerName, messageName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(cancellationToken);

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            _executionCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);
            _logger.LogDebug("Handler {HandlerType} completed successfully in {Duration}ms", context.HandlerName, durationMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            _errorCounter.Add(1, tags);
            _executionDuration.Record(durationMs, tags);
            _logger.LogError(ex, "Handler {HandlerType} failed after {Duration}ms: {ErrorMessage}", context.HandlerName, durationMs, ex.Message);
            throw;
        }
    }
}
