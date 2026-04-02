using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Infrastructure;

public sealed class OutboxMessageBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxMessageBackgroundService> _logger;
    private readonly IMessageSerializer _messageSerializer;

    public OutboxMessageBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<OutboxOptions> options,
        ILogger<OutboxMessageBackgroundService> logger,
        IMessageSerializer messageSerializer)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
        _messageSerializer = messageSerializer;
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
        var commandRider = scope.ServiceProvider.GetRequiredService<ICommandRider>();
        var notificationRider = scope.ServiceProvider.GetRequiredService<INotificationRider>();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();

        try
        {
            var pendingMessages = await repository.AtomicClaimPendingMessagesAsync(_options.BatchSize, _options.ClaimTimeout, cancellationToken)
                .ToListAsync(cancellationToken);

            if (pendingMessages.Count == 0)
            {
                return;
            }

            _logger.LogDebug("Claimed {Count} pending messages across all tenant scopes", pendingMessages.Count);
            await ProcessMessagesBatch(pendingMessages, repository, commandRider, notificationRider, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing outbox messages");
        }
    }

    private async Task ProcessSingleMessageAsync(
        NOFOutboxMessage message,
        IOutboxMessageRepository repository,
        ICommandRider commandRider,
        INotificationRider notificationRider,
        CancellationToken cancellationToken)
    {
        if (message.RetryCount >= _options.MaxRetryCount)
        {
            await repository.AtomicRecordDeliveryFailureAsync(message.Id, $"Exceeded max retry count ({_options.MaxRetryCount})", cancellationToken);
            _logger.LogWarning("Message {MessageId} exceeded max retry count ({MaxRetry}), marked as failed",
                message.Id, _options.MaxRetryCount);
            return;
        }

        // Restore the tracing context
        using var activity = RestoreTracingContext(message);
        var messageId = Guid.NewGuid();
        var payload = _messageSerializer.Deserialize(message.PayloadType, message.Payload);
        var headersTypeInfo = (JsonTypeInfo<Dictionary<string, string?>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Dictionary<string, string?>));
        var headers = string.IsNullOrWhiteSpace(message.Headers)
            ? new Dictionary<string, string?>()
            : JsonSerializer.Deserialize(message.Headers, headersTypeInfo) ?? new Dictionary<string, string?>();

        headers = new Dictionary<string, string?>(headers);
        headers.TryAdd(NOFApplicationConstants.Transport.Headers.MessageId, messageId.ToString());
        headers.TryAdd(NOFApplicationConstants.Transport.Headers.SpanId, activity?.SpanId.ToString());
        headers.TryAdd(NOFApplicationConstants.Transport.Headers.TraceId, activity?.TraceId.ToString());

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(NOFInfrastructureConstants.Messaging.Tags.MessageId, messageId);
            activity.SetTag(NOFInfrastructureConstants.Messaging.Tags.MessageType, payload.GetType().Name);
            activity.SetTag(NOFInfrastructureConstants.Messaging.Tags.Destination, "default");
            activity.SetTag("OutboxMessageId", message.Id);
            if (headers.TryGetValue(NOFApplicationConstants.Transport.Headers.TenantId, out var tenantId))
            {
                activity.SetTag(NOFInfrastructureConstants.Messaging.Tags.TenantId, tenantId);
            }
        }

        try
        {
            if (payload is ICommand command)
            {
                await commandRider.SendAsync(command, headers, cancellationToken);
                _logger.LogDebug("Sent command {MessageId} of type {Type} (retry {Retry})",
                    message.Id, command.GetType().Name, message.RetryCount);
            }
            else if (payload is INotification notification)
            {
                await notificationRider.PublishAsync(notification, headers, cancellationToken);
                _logger.LogDebug("Published notification {MessageId} of type {Type} (retry {Retry})",
                    message.Id, notification.GetType().Name, message.RetryCount);
            }
            else
            {
                await repository.AtomicRecordDeliveryFailureAsync(message.Id, "Unsupported message type", cancellationToken);
                _logger.LogError("Message {MessageId} has unsupported message type: {Type}",
                    message.Id, payload.GetType().FullName ?? "null");
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
            await repository.AtomicRecordDeliveryFailureAsync(message.Id, ex.Message, cancellationToken);
            throw; // ensure not added to success list
        }

        // Processing completed successfully, set Activity status to OK
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    private Activity? RestoreTracingContext(NOFOutboxMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.TraceId))
        {
            return null;
        }

        var traceId = ActivityTraceId.CreateFromString(message.TraceId);
        var spanId = string.IsNullOrWhiteSpace(message.SpanId)
            ? ActivitySpanId.CreateRandom()
            : ActivitySpanId.CreateFromString(message.SpanId);

        var activityContext = new ActivityContext(
            traceId: traceId,
            spanId: spanId,
            traceFlags: ActivityTraceFlags.Recorded,
            isRemote: true);

        var payload = _messageSerializer.Deserialize(message.PayloadType, message.Payload);
        var activity = NOFInfrastructureConstants.Messaging.Source.StartActivity(
            $"{NOFInfrastructureConstants.Messaging.ActivityNames.MessageSending}: {payload.GetType().FullName}",
            kind: ActivityKind.Producer,
            parentContext: activityContext);

        return activity;
    }

    private async Task ProcessMessagesBatch(
        IReadOnlyCollection<NOFOutboxMessage> pendingMessages,
        IOutboxMessageRepository repository,
        ICommandRider commandRider,
        INotificationRider notificationRider,
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
                var tenantId = GetTenantId(message);
                _logger.LogError(ex, "Unhandled exception while processing claimed message {MessageId} for tenant scope {TenantId}", message.Id, tenantId);
            }
        }

        if (succeededIds.Count > 0)
        {
            try
            {
                await repository.AtomicMarkAsSentAsync(succeededIds, cancellationToken);
                _logger.LogInformation(
                    "Outbox batch processed: {Succeeded} sent, {Failed} failed",
                    succeededIds.Count, failedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark {Count} claimed messages as sent", succeededIds.Count);
            }
        }
    }

    private static string GetTenantId(NOFOutboxMessage message)
    {
        var headersTypeInfo = (JsonTypeInfo<Dictionary<string, string?>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Dictionary<string, string?>));
        var headers = string.IsNullOrWhiteSpace(message.Headers)
            ? null
            : JsonSerializer.Deserialize(message.Headers, headersTypeInfo);

        return headers is not null
            && headers.TryGetValue(NOFApplicationConstants.Transport.Headers.TenantId, out var tenantId)
                ? NOFApplicationConstants.Tenant.NormalizeTenantId(tenantId)
                : NOFApplicationConstants.Tenant.HostId;
    }
}
