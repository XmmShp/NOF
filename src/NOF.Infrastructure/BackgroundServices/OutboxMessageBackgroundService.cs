using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class OutboxMessageBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TransactionalMessageProcessorOptions _options;
    private readonly ILogger<OutboxMessageBackgroundService> _logger;
    private readonly IObjectSerializer _objectSerializer;
    private readonly IHostEnvironment _hostEnvironment;

    public OutboxMessageBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<TransactionalMessageOptions> options,
        ILogger<OutboxMessageBackgroundService> logger,
        IObjectSerializer objectSerializer,
        IHostEnvironment hostEnvironment)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value.Outbox;
        _logger = logger;
        _objectSerializer = objectSerializer;
        _hostEnvironment = hostEnvironment;
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
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = scope.ServiceProvider.GetService<IDbContext>();
        if (dbContext is null)
        {
            _logger.LogDebug("Skipping outbox processing because no IDbContext provider is registered.");
            return;
        }
        var commandRider = scope.ServiceProvider.GetRequiredService<ICommandRider>();
        var notificationRider = scope.ServiceProvider.GetRequiredService<INotificationRider>();

        try
        {
            var pendingMessages = await AtomicClaimPendingMessagesAsync(dbContext, _options.BatchSize, _options.ClaimTimeout, cancellationToken)
                .ToListAsync(cancellationToken);

            if (pendingMessages.Count == 0)
            {
                return;
            }

            _logger.LogDebug("Claimed {Count} pending messages across all tenant scopes", pendingMessages.Count);
            await ProcessMessagesBatch(scope.ServiceProvider, dbContext, pendingMessages, commandRider, notificationRider, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing outbox messages");
        }
    }

    private async Task ProcessSingleMessageAsync(
        IServiceProvider scopedServiceProvider,
        IDbContext dbContext,
        NOFOutboxMessage message,
        ICommandRider commandRider,
        INotificationRider notificationRider,
        CancellationToken cancellationToken)
    {
        if (message.RetryCount >= _options.MaxRetryCount)
        {
            await AtomicRecordDeliveryFailureAsync(dbContext, message, $"Exceeded max retry count ({_options.MaxRetryCount})", cancellationToken);
            _logger.LogWarning("Message {MessageId} exceeded max retry count ({MaxRetry}), marked as failed",
                message.Id, _options.MaxRetryCount);
            return;
        }

        // Restore the tracing context
        using var activity = RestoreTracingContext(message);
        var dispatchRoutes = ResolveDispatchRoutes(message);
        var headers = string.IsNullOrWhiteSpace(message.Headers)
            ? new Dictionary<string, string?>()
            : _objectSerializer.Deserialize<Dictionary<string, string?>>(message.Headers) ?? new Dictionary<string, string?>();

        // Restore the ambient execution context for downstream components that rely on it.
        // This keeps "deferred send" semantics consistent: we persist the execution context snapshot,
        // and restore it when actually dispatching the outbox message.
        var currentTenant = scopedServiceProvider.GetRequiredService<IMutableCurrentTenant>();
        var tenantId = headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var headerTenantId)
            ? TenantId.Normalize(headerTenantId)
            : TenantId.Normalize(null);
        using var _ = currentTenant.PushTenant(tenantId);

        activity?.SetTag(NOFInfrastructureConstants.OutboundPipeline.Tags.MessageId, message.Id.ToString());
        activity?.SetTag(NOFInfrastructureConstants.OutboundPipeline.Tags.MessageType, dispatchRoutes[0]);
        if (headers.TryGetValue(NOFAbstractionConstants.Transport.Headers.TenantId, out var activityTenantId))
        {
            activity?.SetTag(NOFInfrastructureConstants.OutboundPipeline.Tags.TenantId, activityTenantId);
        }

        try
        {
            switch (message.MessageType)
            {
                case OutboxMessageType.Command:
                    await commandRider.SendAsync(
                        message.Payload,
                        dispatchRoutes[0],
                        headers,
                        cancellationToken);
                    _logger.LogDebug("Sent command via rider {MessageId} of route {Route} (retry {Retry})",
                        message.Id, dispatchRoutes[0], message.RetryCount);
                    break;
                case OutboxMessageType.Notification:
                    await notificationRider.PublishAsync(
                        message.Payload,
                        dispatchRoutes,
                        headers,
                        cancellationToken);
                    _logger.LogDebug("Published notification via rider {MessageId} with {RouteCount} routes (retry {Retry})",
                        message.Id, dispatchRoutes.Length, message.RetryCount);
                    break;
                default:
                    await AtomicRecordDeliveryFailureAsync(dbContext, message, "Unsupported message type", cancellationToken);
                    _logger.LogError("Message {MessageId} has unsupported message type: {Type}",
                        message.Id, message.MessageType);
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
            await AtomicRecordDeliveryFailureAsync(dbContext, message, ex.Message, cancellationToken);
            throw; // ensure not added to success list
        }

        // Processing completed successfully, set Activity status to OK
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    private Activity? RestoreTracingContext(NOFOutboxMessage message)
    {
        var dispatchRoutes = ResolveDispatchRoutes(message);
        return NOFInfrastructureConstants.OutboundPipeline.Source.StartActivityWithParent(
            $"{NOFInfrastructureConstants.OutboundPipeline.ActivityNames.MessageSending}: {dispatchRoutes[0]}",
            ActivityKind.Producer,
            message.TraceParent,
            _hostEnvironment);
    }

    private string[] ResolveDispatchRoutes(NOFOutboxMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.DispatchRoutes))
        {
            throw new InvalidOperationException($"Outbox message '{message.Id}' does not contain any dispatch routes.");
        }

        var routes = _objectSerializer.Deserialize<string[]>(message.DispatchRoutes);
        if (routes is null || routes.Length == 0)
        {
            throw new InvalidOperationException($"Outbox message '{message.Id}' does not contain any dispatch routes.");
        }

        return routes;
    }

    private async Task ProcessMessagesBatch(
        IServiceProvider scopedServiceProvider,
        IDbContext dbContext,
        IReadOnlyCollection<NOFOutboxMessage> pendingMessages,
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
                await ProcessSingleMessageAsync(scopedServiceProvider, dbContext, message, commandRider, notificationRider, cancellationToken);
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
        IDbContext dbContext,
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

        await TransactionalMessageRecovery.MarkExpiredExhaustedOutboxMessagesAsFailedAsync(
            dbContext,
            _options.MaxRetryCount,
            now,
            cancellationToken);

        var rowsUpdated = await dbContext.Set<NOFOutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Pending &&
                        m.RetryCount < _options.MaxRetryCount &&
                        (m.ClaimedBy == null || m.ClaimExpiresAtUtc == null || m.ClaimExpiresAtUtc <= now))
            .OrderBy(m => m.CreatedAtUtc)
            .Take(batchSize)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.RetryCount, m => m.RetryCount + 1)
                    .SetProperty(m => m.ClaimedBy, lockId)
                    .SetProperty(m => m.ClaimExpiresAtUtc, expiresAt),
                cancellationToken);

        if (rowsUpdated == 0)
        {
            yield break;
        }

        var claimedMessages = await dbContext.Set<NOFOutboxMessage>()
            .AsNoTracking()
            .Where(m => m.ClaimedBy == lockId)
            .ToListAsync(cancellationToken);

        foreach (var msgFromDb in claimedMessages)
        {
            yield return msgFromDb;
        }
    }

    private static async ValueTask AtomicMarkAsSentAsync(
        IDbContext dbContext,
        IEnumerable<Guid> messageIds,
        CancellationToken cancellationToken = default)
    {
        var sentAt = DateTime.UtcNow;

        await dbContext.Set<NOFOutboxMessage>()
            .Where(m => messageIds.Contains(m.Id) && m.Status == OutboxMessageStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxMessageStatus.Sent)
                .SetProperty(m => m.SentAtUtc, sentAt)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAtUtc, (DateTime?)null),
                cancellationToken);
    }

    private async ValueTask AtomicRecordDeliveryFailureAsync(
        IDbContext dbContext,
        NOFOutboxMessage message,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var failedAt = DateTime.UtcNow;
        var rowsUpdated = message.RetryCount >= _options.MaxRetryCount
            ? await dbContext.Set<NOFOutboxMessage>()
                .Where(m => m.Id == message.Id && m.Status == OutboxMessageStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.Status, OutboxMessageStatus.Failed)
                    .SetProperty(m => m.ErrorMessage, errorMessage)
                    .SetProperty(m => m.FailedAtUtc, failedAt)
                    .SetProperty(m => m.ClaimedBy, (string?)null)
                    .SetProperty(m => m.ClaimExpiresAtUtc, (DateTime?)null),
                    cancellationToken)
            : await dbContext.Set<NOFOutboxMessage>()
                .Where(m => m.Id == message.Id && m.Status == OutboxMessageStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.Status, OutboxMessageStatus.Pending)
                    .SetProperty(m => m.ErrorMessage, errorMessage)
                    .SetProperty(m => m.FailedAtUtc, failedAt)
                    .SetProperty(m => m.ClaimedBy, (string?)null)
                    .SetProperty(m => m.ClaimExpiresAtUtc, (DateTime?)null),
                    cancellationToken);

        if (rowsUpdated == 0)
        {
            _logger.LogDebug("Message {MessageId} already processed or not in pending state", message.Id);
            return;
        }

        if (message.RetryCount >= _options.MaxRetryCount)
        {
            _logger.LogWarning(
                "Message {MessageId} marked as permanently failed after {RetryCount} retries. Error: {Error}",
                message.Id, message.RetryCount, errorMessage);
        }
        else
        {
            _logger.LogWarning(
                "Message {MessageId} scheduled for retry #{RetryCount}. Error: {Error}",
                message.Id, message.RetryCount, errorMessage);
        }
    }
}
