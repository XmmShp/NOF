using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Threading.Channels;

namespace NOF;

public sealed class OutboxCommandBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxCommandBackgroundService> _logger;

    public OutboxCommandBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<OutboxOptions> options,
        ILogger<OutboxCommandBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
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
                await Task.Delay(_options.PollingInterval, stoppingToken);
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
        var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContextInternal>();
        var commandRider = scope.ServiceProvider.GetRequiredService<ICommandRider>();
        var notificationRider = scope.ServiceProvider.GetRequiredService<INotificationRider>();

        // 获取所有租户
        var tenants = await tenantRepository.GetAllAsync();
        
        // 保存原始租户上下文
        var originalTenantId = tenantContext.CurrentTenantId;
        
        foreach (var tenant in tenants)
        {
            if (!tenant.IsActive)
            {
                _logger.LogDebug("Skipping inactive tenant {TenantId}", tenant.Id);
                continue;
            }

            try
            {
                // 设置租户上下文
                tenantContext.SetCurrentTenantId(tenant.Id);
                _logger.LogDebug("Processing outbox messages for tenant {TenantId}", tenant.Id);

                // 使用当前 scope 的 repository，它会自动使用设置的租户上下文
                var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();

                // 使用抢占式获取，避免多实例重复处理
                var pendingMessages = await repository.ClaimPendingMessagesAsync(_options.BatchSize, _options.ClaimTimeout, cancellationToken);

                if (pendingMessages.Count == 0)
                {
                    _logger.LogDebug("No pending messages claimed for tenant {TenantId}", tenant.Id);
                    continue;
                }

                _logger.LogDebug("Claimed {Count} pending messages for tenant {TenantId}", pendingMessages.Count, tenant.Id);

                var succeededIds = new List<long>(pendingMessages.Count);
                var failedCount = 0;

                foreach (var message in pendingMessages)
                {
                    try
                    {
                        await ProcessSingleMessageAsync(message, repository, commandRider, notificationRider, cancellationToken);
                        succeededIds.Add(message.Id);
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        _logger.LogError(ex, "Unhandled exception while processing claimed message {MessageId} for tenant {TenantId}", message.Id, tenant.Id);
                    }
                }

                if (succeededIds.Count > 0)
                {
                    try
                    {
                        await repository.MarkAsSentAsync(succeededIds, cancellationToken);
                        _logger.LogInformation(
                            "Tenant {TenantId} batch processed: {Succeeded} sent, {Failed} failed",
                            tenant.Id, succeededIds.Count, failedCount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to mark {Count} claimed messages as sent for tenant {TenantId}", succeededIds.Count, tenant.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages for tenant {TenantId}", tenant.Id);
            }
        }

        // 恢复原始租户上下文
        tenantContext.SetCurrentTenantId(originalTenantId);
    }

    private async Task ProcessSingleMessageAsync(
        OutboxMessage message,
        IOutboxMessageRepository repository,
        ICommandRider commandRider,
        INotificationRider notificationRider,
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
        using var activity = RestoreTracingContext(message);
        var messageId = Guid.NewGuid();

        var headers = new Dictionary<string, string?>(message.Headers);
        headers.TryAdd(NOFConstants.MessageId, messageId.ToString());
        headers.TryAdd(NOFConstants.SpanId, activity?.SpanId.ToString());
        headers.TryAdd(NOFConstants.TraceId, activity?.TraceId.ToString());

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(MessageTracing.Tags.MessageId, messageId);
            activity.SetTag(MessageTracing.Tags.MessageType, message.Message.GetType().Name);
            activity.SetTag(MessageTracing.Tags.Destination, message.DestinationEndpointName ?? "default");
            activity.SetTag("OutboxMessageId", message.Id);
            if (headers.TryGetValue(NOFConstants.TenantId, out var tenantId))
            {
                activity.SetTag(MessageTracing.Tags.TenantId, tenantId);
            }
        }

        try
        {
            if (message.Message is ICommand command)
            {
                await commandRider.SendAsync(command, headers, message.DestinationEndpointName,
                    cancellationToken);
                _logger.LogDebug("Sent command {MessageId} of type {Type} (retry {Retry})",
                    message.Id, command.GetType().Name, message.RetryCount);
            }
            else if (message.Message is INotification notification)
            {
                await notificationRider.PublishAsync(notification, headers, cancellationToken);
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
            activity?.SetStatus(ActivityStatusCode.Ok);
            throw; // rethrow to prevent marking as succeeded
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deliver message {MessageId} (retry {Retry})", message.Id,
                message.RetryCount);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            await repository.RecordDeliveryFailureAsync(message.Id, ex.Message, cancellationToken);
            throw; // ensure not added to success list
        }

        // 成功处理完成，设置 Activity 状态为成功
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    private static Activity? RestoreTracingContext(OutboxMessage message)
    {
        if (message.TraceId is null)
        {
            return null;
        }

        var activityContext = new ActivityContext(
            traceId: message.TraceId ?? ActivityTraceId.CreateRandom(),
            spanId: message.SpanId ?? ActivitySpanId.CreateRandom(),
            traceFlags: ActivityTraceFlags.Recorded,
            isRemote: true);

        var activity = MessageTracing.Source.StartActivity(
            $"{MessageTracing.ActivityNames.MessageSending}: {message.Message.GetType().FullName}",
            kind: ActivityKind.Producer,
            parentContext: activityContext);

        return activity;
    }
}