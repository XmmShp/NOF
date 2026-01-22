using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NOF;

/// <summary>
/// Activity 追踪中间件
/// 为每个 Handler 执行创建分布式追踪 Activity
/// </summary>
public sealed class ActivityTracingMiddleware : IHandlerMiddleware
{
    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        // 合并追踪上下文并创建Activity
        using var activity = RestoreTraceContext(context);

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(HandlerPipelineTracing.Tags.HandlerType, context.HandlerType);
            activity.SetTag(HandlerPipelineTracing.Tags.MessageType, context.MessageType);
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

    /// <summary>
    /// 恢复追踪上下文并创建Activity：如果HandlerContext.Items中有TraceId/SpanId，则恢复追踪上下文
    /// 如果当前已有其它追踪，则尝试合并，合并静默失败
    /// </summary>
    /// <param name="context">Handler执行上下文</param>
    /// <returns>创建的Activity，如果没有追踪信息则返回null</returns>
    private static Activity? RestoreTraceContext(HandlerContext context)
    {
        var traceId = context.Items.GetOrDefault(NOFConstants.TraceId, string.Empty);
        var spanId = context.Items.GetOrDefault(NOFConstants.SpanId, string.Empty);

        var activityContext = new ActivityContext(
            traceId: string.IsNullOrEmpty(traceId) ? ActivityTraceId.CreateRandom() : ActivityTraceId.CreateFromString(traceId),
            spanId: string.IsNullOrEmpty(spanId) ? ActivitySpanId.CreateRandom() : ActivitySpanId.CreateFromString(spanId),
            traceFlags: ActivityTraceFlags.Recorded,
            isRemote: true);

        var activity = HandlerPipelineTracing.Source.StartActivity(
            $"{context.HandlerType}.Handle: {context.MessageType}",
            kind: ActivityKind.Consumer,
            parentContext: activityContext);

        return activity;
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
public sealed class MessageOutboxContextMiddleware : IHandlerMiddleware
{
    private readonly IDeferredCommandSender _deferredCommandSender;
    private readonly IDeferredNotificationPublisher _deferredNotificationPublisher;

    public MessageOutboxContextMiddleware(
        IDeferredCommandSender deferredCommandSender,
        IDeferredNotificationPublisher deferredNotificationPublisher)
    {
        _deferredCommandSender = deferredCommandSender;
        _deferredNotificationPublisher = deferredNotificationPublisher;
    }

    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        using (MessageOutboxContext.BeginScope(_deferredCommandSender, _deferredNotificationPublisher))
        {
            await next(cancellationToken);
        }
    }
}

/// <summary>
/// 收件箱中间件
/// 负责在事务中记录收件箱消息，确保消息的可靠处理
/// </summary>
public sealed class InboxHandlerMiddleware : IHandlerMiddleware
{
    private readonly ITransactionManager _transactionManager;
    private readonly IInboxMessageRepository _inboxMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InboxHandlerMiddleware> _logger;

    public InboxHandlerMiddleware(
        ITransactionManager transactionManager,
        IInboxMessageRepository inboxMessageRepository,
        IUnitOfWork unitOfWork,
        ILogger<InboxHandlerMiddleware> logger)
    {
        _transactionManager = transactionManager;
        _inboxMessageRepository = inboxMessageRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async ValueTask InvokeAsync(HandlerContext context, HandlerDelegate next, CancellationToken cancellationToken)
    {
        await using var transaction = await _transactionManager.BeginTransactionAsync(cancellationToken: cancellationToken);

        try
        {
            var messageId = context.MessageId;
            var messageExists = await _inboxMessageRepository.ExistByMessageIdAsync(messageId, cancellationToken);
            if (messageExists)
            {
                _logger.LogDebug(
                    "Inbox message {MessageId} for {MessageType} already exists, skipping processing",
                    messageId, context.MessageType);

                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            var inboxMessage = new InboxMessage(messageId);

            _inboxMessageRepository.Add(inboxMessage);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await next(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogDebug(
                "Inbox message {MessageId} for {MessageType} processed and committed successfully",
                messageId, context.MessageType);
        }
        catch (Exception ex)
        {
            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx,
                    "Failed to rollback transaction for inbox message processing of {MessageType}",
                    context.MessageType);
            }

            _logger.LogError(ex,
                "Failed to process inbox message for {MessageType}. Transaction has been rolled back.",
                context.MessageType);

            throw;
        }
    }
}
