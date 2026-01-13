using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NOF;

/// <summary>
/// Outbox 消息后台发送服务
/// 定期轮询数据库获取待发送的消息并使用 ICommandSender/INotificationPublisher 发送
/// 使用分布式锁防止多进程重复投递
/// </summary>
public sealed class OutboxCommandBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICacheService _cacheService;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxCommandBackgroundService> _logger;

    public OutboxCommandBackgroundService(
        IServiceProvider serviceProvider,
        ICacheService cacheService,
        IOptions<OutboxOptions> options,
        ILogger<OutboxCommandBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _cacheService = cacheService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Outbox message sender started with polling interval: {Interval}, batch size: {BatchSize}, max retry: {MaxRetry}",
            _options.PollingInterval, _options.BatchSize, _options.MaxRetryCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
                await Task.Delay(_options.PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending messages");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        _logger.LogInformation("Outbox message sender stopped");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITransactionalMessageRepository>();
            var commandSender = scope.ServiceProvider.GetRequiredService<ICommandSender>();
            var notificationPublisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

            var pendingMessages = await repository.GetPendingMessagesAsync(_options.BatchSize, cancellationToken);

            if (pendingMessages.Count == 0)
            {
                return;
            }

            _logger.LogDebug("Found {Count} pending messages", pendingMessages.Count);

            var processedCount = 0;
            var skippedCount = 0;

            foreach (var message in pendingMessages)
            {
                var lockKey = $"outbox:lock:{message.Id}";
                var lockResult = await _cacheService.TryAcquireLockAsync(
                    lockKey,
                    _options.LockExpiration,
                    _options.LockTimeout,
                    cancellationToken);

                if (!lockResult.HasValue)
                {
                    // 锁被其他进程持有，跳过此消息
                    skippedCount++;
                    _logger.LogDebug("Message {MessageId} is locked by another process, skipping", message.Id);
                    continue;
                }

                await using var distributedLock = lockResult.Value;

                try
                {
                    await ProcessSingleMessageAsync(message, repository, commandSender, notificationPublisher, cancellationToken);
                    processedCount++;
                }
                finally
                {
                    // 锁会在 using 结束时自动释放
                }
            }

            if (processedCount > 0 || skippedCount > 0)
            {
                _logger.LogInformation(
                    "Processed {ProcessedCount} messages, skipped {SkippedCount} locked messages",
                    processedCount, skippedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process pending messages");
        }
    }

    private async Task ProcessSingleMessageAsync(
        OutboxMessage message,
        ITransactionalMessageRepository repository,
        ICommandSender commandSender,
        INotificationPublisher notificationPublisher,
        CancellationToken cancellationToken)
    {
        try
        {
            if (message.RetryCount >= _options.MaxRetryCount)
            {
                await repository.MarkAsFailedAsync(message.Id, $"Exceeded max retry count ({_options.MaxRetryCount})", cancellationToken);
                _logger.LogWarning("Message {MessageId} exceeded max retry count ({MaxRetry}), marked as failed", 
                    message.Id, _options.MaxRetryCount);
                return;
            }

            try
            {
                if (message.Message is ICommand command)
                {
                    await commandSender.SendAsync(
                        command,
                        message.DestinationEndpointName,
                        cancellationToken);

                    _logger.LogInformation(
                        "Successfully sent command {MessageId} of type {MessageType} (retry count: {RetryCount})",
                        message.Id, command.GetType().Name, message.RetryCount);
                }
                else if (message.Message is INotification notification)
                {
                    await notificationPublisher.PublishAsync(notification, cancellationToken);

                    _logger.LogInformation(
                        "Successfully published notification {MessageId} of type {MessageType} (retry count: {RetryCount})",
                        message.Id, notification.GetType().Name, message.RetryCount);
                }

                await repository.MarkAsSentAsync([message.Id], cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to send message {MessageId}, retry count: {RetryCount}",
                    message.Id, message.RetryCount);

                await repository.MarkAsFailedAsync(message.Id, ex.Message, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing message {MessageId}", message.Id);
        }
    }
}
