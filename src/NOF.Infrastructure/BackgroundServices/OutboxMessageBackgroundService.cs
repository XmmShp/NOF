using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Application;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class OutboxMessageBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TransactionalMessageProcessorOptions _options;
    private readonly ILogger<OutboxMessageBackgroundService> _logger;
    private readonly IObjectSerializer _objectSerializer;

    public OutboxMessageBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<TransactionalMessageOptions> options,
        ILogger<OutboxMessageBackgroundService> logger,
        IObjectSerializer objectSerializer)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value.Outbox;
        _logger = logger;
        _objectSerializer = objectSerializer;
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
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
        var commandSender = scope.ServiceProvider.GetRequiredService<ICommandSender>();
        var notificationPublisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        try
        {
            var pendingMessages = await AtomicClaimPendingMessagesAsync(dbContext, _options.BatchSize, _options.ClaimTimeout, cancellationToken)
                .ToListAsync(cancellationToken);

            if (pendingMessages.Count == 0)
            {
                return;
            }

            _logger.LogDebug("Claimed {Count} pending messages across all tenant scopes", pendingMessages.Count);
            await ProcessMessagesBatch(scope.ServiceProvider, dbContext, pendingMessages, commandSender, notificationPublisher, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing outbox messages");
        }
    }

    private async Task ProcessSingleMessageAsync(
        IServiceProvider scopedServiceProvider,
        DbContext dbContext,
        NOFOutboxMessage message,
        ICommandSender commandSender,
        INotificationPublisher notificationPublisher,
        CancellationToken cancellationToken)
    {
        if (message.RetryCount >= _options.MaxRetryCount)
        {
            await AtomicRecordDeliveryFailureAsync(dbContext, message.Id, $"Exceeded max retry count ({_options.MaxRetryCount})", cancellationToken);
            _logger.LogWarning("Message {MessageId} exceeded max retry count ({MaxRetry}), marked as failed",
                message.Id, _options.MaxRetryCount);
            return;
        }

        // Restore the tracing context
        using var activity = RestoreTracingContext(message);
        var payloadType = TypeRegistry.Resolve(message.PayloadType);
        var dispatchTypes = ResolveDispatchTypes(message);
        var payload = _objectSerializer.Deserialize(message.Payload, payloadType)!;
        var headers = string.IsNullOrWhiteSpace(message.Headers)
            ? new Dictionary<string, string?>()
            : _objectSerializer.Deserialize<Dictionary<string, string?>>(message.Headers) ?? new Dictionary<string, string?>();

        // Restore the ambient execution context for downstream components that rely on it.
        // This keeps "deferred send" semantics consistent: we persist the execution context snapshot,
        // and restore it when actually dispatching the outbox message.
        var executionContext = scopedServiceProvider.GetRequiredService<ITransparentInfos>();
        executionContext.ReplaceHeadersFrom(headers);

        activity?.SetTag(NOFInfrastructureConstants.OutboundPipeline.Tags.MessageId, message.Id.ToString());
        activity?.SetTag(NOFInfrastructureConstants.OutboundPipeline.Tags.MessageType, payload.GetType().Name);
        if (headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var tenantId))
        {
            activity?.SetTag(NOFInfrastructureConstants.OutboundPipeline.Tags.TenantId, tenantId);
        }

        try
        {
            switch (message.MessageType)
            {
                case OutboxMessageType.Command:
                    await commandSender.SendAsync(payload, dispatchTypes[0], cancellationToken);
                    _logger.LogDebug("Sent command via sender {MessageId} of type {Type} (retry {Retry})",
                        message.Id, payload.GetType().Name, message.RetryCount);
                    break;
                case OutboxMessageType.Notification:
                    await notificationPublisher.PublishAsync(payload, dispatchTypes, cancellationToken);
                    _logger.LogDebug("Published notification via publisher {MessageId} of type {Type} (retry {Retry})",
                        message.Id, payload.GetType().Name, message.RetryCount);
                    break;
                default:
                    await AtomicRecordDeliveryFailureAsync(dbContext, message.Id, "Unsupported message type", cancellationToken);
                    _logger.LogError("Message {MessageId} has unsupported message type: {Type}",
                        message.Id, payload.GetType().FullName ?? "null");
                    break;
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
            await AtomicRecordDeliveryFailureAsync(dbContext, message.Id, ex.Message, cancellationToken);
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

    private Type[] ResolveDispatchTypes(NOFOutboxMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.DispatchTypes))
        {
            return [TypeRegistry.Resolve(message.PayloadType)];
        }

        var typeNames = _objectSerializer.Deserialize<string[]>(message.DispatchTypes);
        if (typeNames is null || typeNames.Length == 0)
        {
            return [TypeRegistry.Resolve(message.PayloadType)];
        }

        return [.. typeNames.Select(TypeRegistry.Resolve)];
    }

    private async Task ProcessMessagesBatch(
        IServiceProvider scopedServiceProvider,
        DbContext dbContext,
        IReadOnlyCollection<NOFOutboxMessage> pendingMessages,
        ICommandSender commandSender,
        INotificationPublisher notificationPublisher,
        CancellationToken cancellationToken)
    {
        var succeededIds = new List<Guid>(pendingMessages.Count);
        var failedCount = 0;

        foreach (var message in pendingMessages)
        {
            try
            {
                await ProcessSingleMessageAsync(scopedServiceProvider, dbContext, message, commandSender, notificationPublisher, cancellationToken);
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
                await AtomicMarkAsSentAsync(dbContext, succeededIds, cancellationToken);
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

    private async IAsyncEnumerable<NOFOutboxMessage> AtomicClaimPendingMessagesAsync(
        DbContext dbContext,
        int batchSize = 100,
        TimeSpan? claimTimeout = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            batchSize = _options.BatchSize;
        }

        var timeout = claimTimeout ?? _options.ClaimTimeout;
        var lockId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(timeout);

        var rowsUpdated = await dbContext.Set<NOFOutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Pending &&
                        m.RetryCount < _options.MaxRetryCount &&
                        (m.ClaimedBy == null || m.ClaimExpiresAt == null || m.ClaimExpiresAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.RetryCount, m => m.RetryCount + 1)
                    .SetProperty(m => m.ClaimedBy, lockId)
                    .SetProperty(m => m.ClaimExpiresAt, expiresAt),
                cancellationToken);

        if (rowsUpdated == 0)
        {
            yield break;
        }

        var claimedMessagesFromDb = await dbContext.Set<NOFOutboxMessage>()
            .AsNoTracking()
            .Where(m => m.ClaimedBy == lockId)
            .ToListAsync(cancellationToken);

        foreach (var msgFromDb in claimedMessagesFromDb)
        {
            var trackedEntry = dbContext.ChangeTracker.Entries<NOFOutboxMessage>()
                .FirstOrDefault(e => e.Entity.Id == msgFromDb.Id);

            if (trackedEntry != null)
            {
                await trackedEntry.ReloadAsync(cancellationToken);
                yield return trackedEntry.Entity;
            }
            else
            {
                dbContext.Attach(msgFromDb);
                yield return msgFromDb;
            }
        }
    }

    private static async ValueTask AtomicMarkAsSentAsync(
        DbContext dbContext,
        IEnumerable<Guid> messageIds,
        CancellationToken cancellationToken = default)
    {
        var sentAt = DateTime.UtcNow;

        await dbContext.Set<NOFOutboxMessage>()
            .Where(m => messageIds.Contains(m.Id) && m.Status == OutboxMessageStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxMessageStatus.Sent)
                .SetProperty(m => m.SentAt, sentAt)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAt, (DateTime?)null),
                cancellationToken);
    }

    private async ValueTask AtomicRecordDeliveryFailureAsync(
        DbContext dbContext,
        Guid messageId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var failedAt = DateTime.UtcNow;
        var rowsUpdated = await dbContext.Set<NOFOutboxMessage>()
            .Where(m => m.Id == messageId && m.Status == OutboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.ErrorMessage, errorMessage)
                .SetProperty(m => m.FailedAt, failedAt),
                cancellationToken);

        if (rowsUpdated == 0)
        {
            _logger.LogDebug("Message {MessageId} already processed or not in pending state", messageId);
            return;
        }

        NOFOutboxMessage? message;
        var trackedEntry = dbContext.ChangeTracker.Entries<NOFOutboxMessage>().FirstOrDefault(e => e.Entity.Id == messageId);
        if (trackedEntry != null)
        {
            await trackedEntry.ReloadAsync(cancellationToken);
            message = trackedEntry.Entity;
        }
        else
        {
            message = await dbContext.Set<NOFOutboxMessage>()
                .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);
        }

        if (message == null)
        {
            return;
        }

        if (message.RetryCount >= _options.MaxRetryCount)
        {
            message.Status = OutboxMessageStatus.Failed;
            message.ClaimedBy = null;
            message.ClaimExpiresAt = null;
            _logger.LogWarning(
                "Message {MessageId} marked as permanently failed after {RetryCount} retries. Error: {Error}",
                messageId, message.RetryCount, errorMessage);
        }
        else
        {
            message.Status = OutboxMessageStatus.Pending;
            message.ClaimedBy = null;
            message.ClaimExpiresAt = null;

            _logger.LogWarning(
                "Message {MessageId} scheduled for retry #{RetryCount}. Error: {Error}",
                messageId, message.RetryCount, errorMessage);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
