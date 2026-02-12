using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using System.Diagnostics;

namespace NOF.Infrastructure.Core;

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
        var invocationContext = scope.ServiceProvider.GetRequiredService<IInvocationContextInternal>();
        var commandRider = scope.ServiceProvider.GetRequiredService<ICommandRider>();
        var notificationRider = scope.ServiceProvider.GetRequiredService<INotificationRider>();

        // Save the original tenant context
        var originalTenantId = invocationContext.TenantId;

        // Process Host database first (TenantId = null)
        try
        {
            invocationContext.SetTenantId(null);
            _logger.LogDebug("Processing outbox messages for Host database");

            var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
            var pendingMessages = await repository.ClaimPendingMessagesAsync(_options.BatchSize, _options.ClaimTimeout, cancellationToken);

            if (pendingMessages.Count > 0)
            {
                _logger.LogDebug("Claimed {Count} pending messages for Host database", pendingMessages.Count);
                await ProcessMessagesBatch(pendingMessages, repository, commandRider, notificationRider, "Host", null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing outbox messages for Host database");
        }

        // Process all tenant databases
        var tenants = await tenantRepository.GetAllAsync();

        foreach (var tenant in tenants)
        {
            if (!tenant.IsActive)
            {
                _logger.LogDebug("Skipping inactive tenant {TenantId}", tenant.Id);
                continue;
            }

            try
            {
                // Set the tenant context
                invocationContext.SetTenantId(tenant.Id);
                _logger.LogDebug("Processing outbox messages for tenant {TenantId}", tenant.Id);

                // Use the current scope's repository, which automatically uses the set tenant context
                var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();

                // Use claim-based retrieval to avoid duplicate processing across instances
                var pendingMessages = await repository.ClaimPendingMessagesAsync(_options.BatchSize, _options.ClaimTimeout, cancellationToken);

                if (pendingMessages.Count == 0)
                {
                    _logger.LogDebug("No pending messages claimed for tenant {TenantId}", tenant.Id);
                    continue;
                }

                _logger.LogDebug("Claimed {Count} pending messages for tenant {TenantId}", pendingMessages.Count, tenant.Id);
                await ProcessMessagesBatch(pendingMessages, repository, commandRider, notificationRider, "Tenant", tenant.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages for tenant {TenantId}", tenant.Id);
            }
        }

        // Restore the original tenant context
        invocationContext.SetTenantId(originalTenantId);
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

        // Restore the tracing context
        using var activity = RestoreTracingContext(message);
        var messageId = Guid.NewGuid();

        var headers = new Dictionary<string, string?>(message.Headers);
        headers.TryAdd(NOFInfrastructureCoreConstants.Transport.Headers.MessageId, messageId.ToString());
        headers.TryAdd(NOFInfrastructureCoreConstants.Transport.Headers.SpanId, activity?.SpanId.ToString());
        headers.TryAdd(NOFInfrastructureCoreConstants.Transport.Headers.TraceId, activity?.TraceId.ToString());

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(MessageTracing.Tags.MessageId, messageId);
            activity.SetTag(MessageTracing.Tags.MessageType, message.Message.GetType().Name);
            activity.SetTag(MessageTracing.Tags.Destination, message.DestinationEndpointName ?? "default");
            activity.SetTag("OutboxMessageId", message.Id);
            if (headers.TryGetValue(NOFInfrastructureCoreConstants.Transport.Headers.TenantId, out var tenantId))
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
            // Service is shutting down, do not record as failure; leave for retry on next startup
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

        // Processing completed successfully, set Activity status to OK
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

    private async Task ProcessMessagesBatch(
        IReadOnlyCollection<OutboxMessage> pendingMessages,
        IOutboxMessageRepository repository,
        ICommandRider commandRider,
        INotificationRider notificationRider,
        string contextType,
        string? tenantId,
        CancellationToken cancellationToken)
    {
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
                if (tenantId != null)
                {
                    _logger.LogError(ex, "Unhandled exception while processing claimed message {MessageId} for tenant {TenantId}", message.Id, tenantId);
                }
                else
                {
                    _logger.LogError(ex, "Unhandled exception while processing claimed message {MessageId} for Host database", message.Id);
                }
            }
        }

        if (succeededIds.Count > 0)
        {
            try
            {
                await repository.MarkAsSentAsync(succeededIds, cancellationToken);
                if (tenantId != null)
                {
                    _logger.LogInformation(
                        "Tenant {TenantId} batch processed: {Succeeded} sent, {Failed} failed",
                        tenantId, succeededIds.Count, failedCount);
                }
                else
                {
                    _logger.LogInformation(
                        "Host database batch processed: {Succeeded} sent, {Failed} failed",
                        succeededIds.Count, failedCount);
                }
            }
            catch (Exception ex)
            {
                if (tenantId != null)
                {
                    _logger.LogError(ex, "Failed to mark {Count} claimed messages as sent for tenant {TenantId}", succeededIds.Count, tenantId);
                }
                else
                {
                    _logger.LogError(ex, "Failed to mark {Count} claimed messages as sent for Host database", succeededIds.Count);
                }
            }
        }
    }
}
