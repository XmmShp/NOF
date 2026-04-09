using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Infrastructure;

public sealed class OutboxMessageBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxMessageBackgroundService> _logger;
    private readonly IObjectSerializer _objectSerializer;
    private readonly ICommandRider _commandRider;
    private readonly INotificationRider _notificationRider;

    public OutboxMessageBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<OutboxOptions> options,
        ILogger<OutboxMessageBackgroundService> logger,
        IObjectSerializer objectSerializer)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
        _objectSerializer = objectSerializer;
        _commandRider = serviceProvider.GetRequiredService<ICommandRider>();
        _notificationRider = serviceProvider.GetRequiredService<INotificationRider>();
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
            await ProcessMessagesBatch(pendingMessages, repository, _commandRider, _notificationRider, cancellationToken);
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
        var payloadType = TypeRegistry.Resolve(message.PayloadType);
        var payload = _objectSerializer.Deserialize(message.Payload, payloadType)!;
        var headersTypeInfo = (JsonTypeInfo<Dictionary<string, string?>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Dictionary<string, string?>));
        var headers = string.IsNullOrWhiteSpace(message.Headers)
            ? new Dictionary<string, string?>()
            : JsonSerializer.Deserialize(message.Headers, headersTypeInfo) ?? new Dictionary<string, string?>();

        // Add additional headers
        headers[NOFContractConstants.Transport.Headers.MessageId] = message.Id.ToString();
        headers[NOFContractConstants.Transport.Headers.SpanId] = activity?.SpanId.ToString();
        headers[NOFContractConstants.Transport.Headers.TraceId] = activity?.TraceId.ToString();

        activity?.SetTag(NOFInfrastructureConstants.OutboundPipeline.Tags.MessageId, message.Id.ToString());
        activity?.SetTag(NOFInfrastructureConstants.OutboundPipeline.Tags.MessageType, payload.GetType().Name);
        if (headers.TryGetValue(NOFContractConstants.Transport.Headers.TenantId, out var tenantId))
        {
            activity?.SetTag(NOFInfrastructureConstants.OutboundPipeline.Tags.TenantId, tenantId);
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
        var payloadType = TypeRegistry.Resolve(message.PayloadType);
        var payload = _objectSerializer.Deserialize(message.Payload, payloadType)!;
        var parent = message.ParentTracingInfo;
        return NOFInfrastructureConstants.OutboundPipeline.Source.StartActivityWithParent(
            $"{NOFInfrastructureConstants.OutboundPipeline.ActivityNames.MessageSending}: {payload.GetType().FullName}",
            ActivityKind.Producer,
            parent);
    }

    private async Task ProcessMessagesBatch(
        IReadOnlyCollection<NOFOutboxMessage> pendingMessages,
        IOutboxMessageRepository repository,
        ICommandRider commandRider,
        INotificationRider notificationRider,
        CancellationToken cancellationToken)
    {
        var succeededIds = new List<Guid>(pendingMessages.Count);
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
                _logger.LogError(ex, "Unhandled exception while processing claimed message {MessageId}", message.Id);
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
}
