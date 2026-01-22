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
        // 从HandlerContext.Items中提取追踪信息
        if (!context.Items.TryGetValue(NOFConstants.TraceId, out var traceIdObj) ||
            traceIdObj is not string traceId ||
            string.IsNullOrEmpty(traceId))
        {
            // 没有TraceId，创建普通的Handler Activity
            var activity = HandlerPipelineTracing.Source.StartActivity(
                $"{context.HandlerType}.Handle",
                ActivityKind.Internal);

            if (activity != null)
            {
                activity.SetTag(HandlerPipelineTracing.Tags.HandlerType, context.HandlerType);
                activity.SetTag(HandlerPipelineTracing.Tags.MessageType, context.MessageType);
                activity.SetTag(HandlerPipelineTracing.Tags.MessageName, GetMessageName(context.MessageType));
            }

            return activity;
        }

        var currentActivity = Activity.Current;

        // 如果当前没有Activity，创建一个新的Activity来恢复追踪上下文
        if (currentActivity == null)
        {
            var spanId = ActivitySpanId.CreateRandom();
            if (context.Items.TryGetValue(NOFConstants.SpanId, out var spanIdObj1) &&
                spanIdObj1 is string spanIdStr1 &&
                !string.IsNullOrEmpty(spanIdStr1))
            {
                spanId = ActivitySpanId.CreateFromString(spanIdStr1.AsSpan());
            }

            var activityContext = new ActivityContext(
                traceId: ActivityTraceId.CreateFromString(traceId.AsSpan()),
                spanId: spanId,
                traceFlags: ActivityTraceFlags.Recorded,
                isRemote: true);

            var activity = HandlerPipelineTracing.Source.StartActivity(
                $"{context.HandlerType}.Handle",
                kind: ActivityKind.Consumer,
                parentContext: activityContext);

            if (activity is { IsAllDataRequested: true })
            {
                activity.SetTag("trace.id", traceId);
                activity.SetTag("span.id", spanId.ToString());
                activity.SetTag(HandlerPipelineTracing.Tags.HandlerType, context.HandlerType);
                activity.SetTag(HandlerPipelineTracing.Tags.MessageType, context.MessageType);
                activity.SetTag(HandlerPipelineTracing.Tags.MessageName, GetMessageName(context.MessageType));
            }

            return activity;
        }

        // 如果当前已有Activity，尝试合并（静默失败）
        // 设置TraceId和SpanId到Activity的Tag中，以便追踪
        currentActivity.SetTag("trace.id", traceId);

        if (context.Items.TryGetValue(NOFConstants.SpanId, out var spanIdObj) &&
            spanIdObj is string spanIdStr &&
            !string.IsNullOrEmpty(spanIdStr))
        {
            currentActivity.SetTag("span.id", spanIdStr);
        }

        // 创建普通的Handler Activity，但会继承当前Activity的上下文
        var handlerActivity = HandlerPipelineTracing.Source.StartActivity(
            $"{context.HandlerType}.Handle",
            ActivityKind.Internal);

        if (handlerActivity != null)
        {
            handlerActivity.SetTag(HandlerPipelineTracing.Tags.HandlerType, context.HandlerType);
            handlerActivity.SetTag(HandlerPipelineTracing.Tags.MessageType, context.MessageType);
            handlerActivity.SetTag(HandlerPipelineTracing.Tags.MessageName, GetMessageName(context.MessageType));
        }

        return handlerActivity;
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

            // 创建收件箱消息
            var inboxMessage = new InboxMessage(messageId);

            // 添加收件箱消息到仓储
            _inboxMessageRepository.Add(inboxMessage);

            // 保存更改到事务中
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 执行后续逻辑
            await next(cancellationToken);

            // 提交事务
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
