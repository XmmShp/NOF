using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Threading.Channels;

namespace NOF;

public interface IOutboxPublisher
{
    void TriggerImmediateProcessing();
}

public sealed class OutboxCommandBackgroundService : BackgroundService, IOutboxPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxCommandBackgroundService> _logger;
    private readonly Channel<object> _wakeUpChannel;

    public OutboxCommandBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<OutboxOptions> options,
        ILogger<OutboxCommandBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;

        _wakeUpChannel = Channel.CreateBounded<object>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public void TriggerImmediateProcessing()
    {
        _wakeUpChannel.Writer.TryWrite(null!);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Outbox message sender started. PollingInterval: {Interval}, BatchSize: {BatchSize}, MaxRetry: {MaxRetry}",
            _options.PollingInterval, _options.BatchSize, _options.MaxRetryCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var wakeUpTask = _wakeUpChannel.Reader.WaitToReadAsync(stoppingToken).AsTask();
                var delayTask = Task.Delay(_options.PollingInterval, stoppingToken);

                var completedTask = await Task.WhenAny(wakeUpTask, delayTask);

                if (completedTask == wakeUpTask && wakeUpTask.Result)
                {
                    // 消费所有唤醒信号（防止多次 WakeUp 被丢弃）
                    while (_wakeUpChannel.Reader.TryRead(out _))
                    { }
                    _logger.LogDebug("Received wake-up signal(s), processing immediately");
                }

                await ProcessPendingMessagesAsync(stoppingToken);

            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in outbox background service loop");
            }
        }

        _logger.LogInformation("Outbox message sender stopped");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionalMessageRepository>();
        var commandSender = scope.ServiceProvider.GetRequiredService<ICommandSender>();
        var notificationPublisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        // 使用抢占式获取，避免多实例重复处理
        var pendingMessages = await repository.ClaimPendingMessagesAsync(_options.BatchSize, _options.ClaimTimeout, cancellationToken);

        if (pendingMessages.Count == 0)
        {
            _logger.LogDebug("No pending messages claimed for processing");
            return;
        }

        _logger.LogDebug("Claimed {Count} pending messages for processing", pendingMessages.Count);

        var succeededIds = new List<Guid>(pendingMessages.Count);
        var failedCount = 0;

        foreach (var message in pendingMessages)
        {
            try
            {
                await ProcessSingleMessageAsync(message, repository, commandSender, notificationPublisher, cancellationToken);
                succeededIds.Add(message.Id);
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogError(ex, "Unhandled exception while processing claimed message {MessageId}", message.Id);
            }
        }

        if (succeededIds.Count > 0)
        {
            try
            {
                await repository.MarkAsSentAsync(succeededIds, cancellationToken);
                _logger.LogInformation(
                    "Batch processed: {Succeeded} sent, {Failed} failed",
                    succeededIds.Count, failedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark {Count} claimed messages as sent", succeededIds.Count);
            }
        }
    }

    private async Task ProcessSingleMessageAsync(
        OutboxMessage message,
        ITransactionalMessageRepository repository,
        ICommandSender commandSender,
        INotificationPublisher notificationPublisher,
        CancellationToken cancellationToken)
    {
        if (message.RetryCount >= _options.MaxRetryCount)
        {
            await repository.RecordDeliveryFailureAsync(message.Id, $"Exceeded max retry count ({_options.MaxRetryCount})", cancellationToken);
            _logger.LogWarning("Message {MessageId} exceeded max retry count ({MaxRetry}), marked as failed",
                message.Id, _options.MaxRetryCount);
            return;
        }

        // 恢复追踪上下文
        using var restoredActivity = RestoreTracingContext(message);

        try
        {
            if (message.Message is ICommand command)
            {
                await commandSender.SendAsync(command, message.DestinationEndpointName, cancellationToken);
                _logger.LogDebug("Sent command {MessageId} of type {Type} (retry {Retry})",
                    message.Id, command.GetType().Name, message.RetryCount);
            }
            else if (message.Message is INotification notification)
            {
                await notificationPublisher.PublishAsync(notification, cancellationToken);
                _logger.LogDebug("Published notification {MessageId} of type {Type} (retry {Retry})",
                    message.Id, notification.GetType().Name, message.RetryCount);
            }
            else
            {
                await repository.RecordDeliveryFailureAsync(message.Id, "Unsupported message type", cancellationToken);
                _logger.LogError("Message {MessageId} has unsupported message type: {Type}",
                    message.Id, message.Message.GetType().FullName ?? "null");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 服务正在关闭，不记录为失败，留待下次启动重试
            _logger.LogInformation("Message {MessageId} delivery canceled due to shutdown", message.Id);
            throw; // rethrow to prevent marking as succeeded
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deliver message {MessageId} (retry {Retry})", message.Id, message.RetryCount);
            await repository.RecordDeliveryFailureAsync(message.Id, ex.Message, cancellationToken);
            throw; // ensure not added to success list
        }
    }

    private static Activity? RestoreTracingContext(OutboxMessage message)
    {
        if (string.IsNullOrEmpty(message.TraceId))
        {
            return null;
        }

        var activityContext = new ActivityContext(
            traceId: ActivityTraceId.CreateFromString(message.TraceId.AsSpan()),
            spanId: string.IsNullOrEmpty(message.SpanId) ? ActivitySpanId.CreateRandom() : ActivitySpanId.CreateFromString(message.SpanId.AsSpan()),
            traceFlags: ActivityTraceFlags.Recorded,
            isRemote: true);

        var activity = OutboxTracing.Source.StartActivity(
            OutboxTracing.ActivityNames.MessageProcessing,
            kind: ActivityKind.Consumer,
            parentContext: activityContext);

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(OutboxTracing.Tags.MessageId, message.Id.ToString());
            activity.SetTag(OutboxTracing.Tags.MessageType, message.Message.GetType().Name);
            activity.SetTag(OutboxTracing.Tags.RetryCount, message.RetryCount.ToString());
            activity.SetTag(OutboxTracing.Tags.TraceId, message.TraceId);
            if (!string.IsNullOrEmpty(message.SpanId))
            {
                activity.SetTag(OutboxTracing.Tags.SpanId, message.SpanId);
            }
        }

        return activity;
    }
}