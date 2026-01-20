using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NOF;

/// <summary>
/// Handler 执行上下文
/// 包含 Handler 执行过程中的元数据
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class HandlerContext
{
    /// <summary>
    /// Handler 类型名称
    /// </summary>
    public required string HandlerType { get; init; }

    /// <summary>
    /// 消息类型名称
    /// </summary>
    public required string MessageType { get; init; }

    /// <summary>
    /// 消息实例
    /// </summary>
    public required object Message { get; init; }

    /// <summary>
    /// Handler 实例
    /// </summary>
    public required object Handler { get; init; }

    /// <summary>
    /// 自定义属性字典，用于在中间件之间传递数据
    /// </summary>
    public Dictionary<string, object> Items { get; } = new();
}

/// <summary>
/// Handler 执行管道的委托
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ValueTask HandlerDelegate(CancellationToken cancellationToken);

/// <summary>
/// Handler 中间件接口
/// 用于在 Handler 执行前后插入横切关注点（如事务、日志、验证等）
/// </summary>
public interface IHandlerMiddleware
{
    /// <summary>
    /// 执行中间件逻辑
    /// </summary>
    /// <param name="context">Handler 执行上下文</param>
    /// <param name="next">管道中的下一个中间件或最终的 Handler</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken);
}

/// <summary>
/// Activity 追踪中间件
/// 为每个 Handler 执行创建分布式追踪 Activity
/// </summary>
public sealed class ActivityTracingMiddleware : IHandlerMiddleware
{
    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        using var activity = HandlerPipelineTracing.Source.StartActivity(
            $"{context.HandlerType}.Handle",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag(HandlerPipelineTracing.Tags.HandlerType, context.HandlerType);
            activity.SetTag(HandlerPipelineTracing.Tags.MessageType, context.MessageType);
            activity.SetTag(HandlerPipelineTracing.Tags.MessageName, GetMessageName(context.MessageType));
        }

        try
        {
            await next(cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.AddException(ex);
            }
            throw;
        }
    }

    private static string GetMessageName(string fullTypeName)
    {
        var lastDot = fullTypeName.LastIndexOf('.');
        return lastDot >= 0 ? fullTypeName.Substring(lastDot + 1) : fullTypeName;
    }
}

/// <summary>
/// 自动埋点中间件
/// 自动记录 Handler 执行的日志、指标和性能数据
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

/// <summary>
/// 事务性消息上下文中间件
/// 为 Command Handler 自动创建 TransactionalMessageContext Scope
/// </summary>
public sealed class TransactionalMessageContextMiddleware : IHandlerMiddleware
{
    private readonly IDeferredCommandSender _deferredCommandSender;
    private readonly IDeferredNotificationPublisher _deferredNotificationPublisher;

    public TransactionalMessageContextMiddleware(
        IDeferredCommandSender deferredCommandSender,
        IDeferredNotificationPublisher deferredNotificationPublisher)
    {
        _deferredCommandSender = deferredCommandSender;
        _deferredNotificationPublisher = deferredNotificationPublisher;
    }

    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        using (TransactionalMessageContext.BeginScope(_deferredCommandSender, _deferredNotificationPublisher))
        {
            await next(cancellationToken);
        }
    }
}
