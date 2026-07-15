using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Application;
using System.Diagnostics;

namespace NOF.Infrastructure;

public sealed class InboxMessageBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TransactionalMessageProcessorOptions _options;
    private readonly ILogger<InboxMessageBackgroundService> _logger;
    private readonly IObjectSerializer _objectSerializer;

    public InboxMessageBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<TransactionalMessageOptions> options,
        ILogger<InboxMessageBackgroundService> logger,
        IObjectSerializer objectSerializer)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value.Inbox;
        _logger = logger;
        _objectSerializer = objectSerializer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Inbox message processor started. PollingInterval: {Interval}, BatchSize: {BatchSize}, MaxRetry: {MaxRetry}",
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
                _logger.LogError(ex, "Unexpected error in inbox background service loop");
            }
        }

        _logger.LogInformation("Inbox message processor stopped");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = scope.ServiceProvider.GetService<IDbContext>();
        if (dbContext is null)
        {
            _logger.LogDebug("Skipping inbox processing because no IDbContext provider is registered.");
            return;
        }

        var pendingMessages = await AtomicClaimPendingMessagesAsync(dbContext, _options.BatchSize, _options.ClaimTimeout, cancellationToken)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        var succeededIds = new List<Guid>(pendingMessages.Count);
        var failedCount = 0;

        foreach (var message in pendingMessages)
        {
            try
            {
                await ProcessSingleMessageAsync(scope.ServiceProvider, message, cancellationToken);
                succeededIds.Add(message.Id);
            }
            catch (Exception)
            {
                failedCount++;
                // Failure details are recorded in the inbox row (retry/failed) and logged at the decision point.
            }
        }

        _logger.LogInformation(
            "Inbox batch processed: {Succeeded} processed, {Failed} failed",
            succeededIds.Count,
            failedCount);
    }

    private async Task ProcessSingleMessageAsync(
        IServiceProvider services,
        NOFInboxMessage message,
        CancellationToken cancellationToken)
    {
        if (message.Status != InboxMessageStatus.Pending)
        {
            return;
        }

        if (message.RetryCount > _options.MaxRetryCount)
        {
            await MarkFailedAsync(message.Id, message.HandlerType, message.RetryCount, "Exceeded max retry count", cancellationToken);
            return;
        }

        var headers = DeserializeHeaders(message.Headers);
        var traceParent = ExtractTraceParent(headers);
        var processingHeaders = RemoveTraceParent(headers);
        var handlerType = TypeResolver.ResolveHandler(message.HandlerType);

        try
        {
            using var activity = StartBoundaryActivity(message, traceParent);

            switch (message.MessageType)
            {
                case InboxMessageType.Command:
                    {
                        var commandPipelineExecutor = services.GetRequiredService<CommandInboundPipelineExecutor>();
                        await commandPipelineExecutor.ExecuteAsync(
                            message.Payload,
                            message.PayloadType,
                            handlerType,
                            processingHeaders,
                            cancellationToken);
                        break;
                    }
                case InboxMessageType.Notification:
                    {
                        var notificationPipelineExecutor = services.GetRequiredService<NotificationInboundPipelineExecutor>();
                        await notificationPipelineExecutor.ExecuteAsync(
                            message.Payload,
                            message.PayloadType,
                            handlerType,
                            processingHeaders,
                            cancellationToken);
                        break;
                    }
                default:
                    throw new InvalidOperationException($"Unsupported inbox message type '{message.MessageType}'.");
            }

            await MarkProcessedAsync(message.Id, message.HandlerType, cancellationToken);
        }
        catch (Exception ex)
        {
            if (message.RetryCount >= _options.MaxRetryCount)
            {
                await MarkFailedAsync(message.Id, message.HandlerType, message.RetryCount, ex.Message, cancellationToken, ex);
            }
            else
            {
                await MarkRetryAsync(message.Id, message.HandlerType, message.RetryCount, ex.Message, cancellationToken);
            }

            throw;
        }
    }

    private async Task MarkProcessedAsync(Guid messageId, string handlerType, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var processedAt = DateTime.UtcNow;

        await dbContext.Set<NOFInboxMessage>()
            .Where(m => m.Id == messageId && m.HandlerType == handlerType && m.Status == InboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, InboxMessageStatus.Processed)
                .SetProperty(m => m.ProcessedAtUtc, processedAt)
                .SetProperty(m => m.ErrorMessage, (string?)null)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAtUtc, (DateTime?)null),
                cancellationToken);
    }

    private async Task MarkRetryAsync(Guid messageId, string handlerType, int retryCount, string error, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();

        await dbContext.Set<NOFInboxMessage>()
            .Where(m => m.Id == messageId && m.HandlerType == handlerType && m.Status == InboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.ErrorMessage, error)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAtUtc, (DateTime?)null),
                cancellationToken);

        _logger.LogWarning(
            "Inbox message {InboxId} scheduled for retry #{RetryCount}. Error: {Error}",
            messageId,
            retryCount,
            error);
    }

    private async Task MarkFailedAsync(Guid messageId, string handlerType, int retryCount, string error, CancellationToken cancellationToken, Exception? ex = null)
    {
        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var failedAt = DateTime.UtcNow;

        await dbContext.Set<NOFInboxMessage>()
            .Where(m => m.Id == messageId && m.HandlerType == handlerType && m.Status == InboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, InboxMessageStatus.Failed)
                .SetProperty(m => m.ErrorMessage, error)
                .SetProperty(m => m.FailedAtUtc, failedAt)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAtUtc, (DateTime?)null),
                cancellationToken);

        if (ex is null)
        {
            _logger.LogError(
                "Inbox message {InboxId} marked as permanently failed after {RetryCount} retries. Error: {Error}",
                messageId,
                retryCount,
                error);
        }
        else
        {
            _logger.LogError(
                ex,
                "Inbox message {InboxId} marked as permanently failed after {RetryCount} retries. Error: {Error}",
                messageId,
                retryCount,
                error);
        }
    }

    private async IAsyncEnumerable<NOFInboxMessage> AtomicClaimPendingMessagesAsync(
        IDbContext dbContext,
        int batchSize,
        TimeSpan claimTimeout,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            batchSize = _options.BatchSize;
        }

        var lockId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(claimTimeout);

        await TransactionalMessageRecovery.MarkExpiredExhaustedInboxMessagesAsFailedAsync(
            dbContext,
            _options.MaxRetryCount,
            now,
            cancellationToken);

        var rowsUpdated = await dbContext.Set<NOFInboxMessage>()
            .Where(m => m.Status == InboxMessageStatus.Pending &&
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

        var claimed = await dbContext.Set<NOFInboxMessage>()
            .AsNoTracking()
            .Where(m => m.ClaimedBy == lockId)
            .ToListAsync(cancellationToken);

        foreach (var msgFromDb in claimed)
        {
            yield return msgFromDb;
        }
    }

    private IEnumerable<KeyValuePair<string, string?>> DeserializeHeaders(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<KeyValuePair<string, string?>>();
        }

        var dict = _objectSerializer.Deserialize<Dictionary<string, string?>>(raw) ?? new Dictionary<string, string?>();
        return dict;
    }

    private static string? ExtractTraceParent(IEnumerable<KeyValuePair<string, string?>> headers)
        => headers.FirstOrDefault(static kvp => kvp.Key == NOFAbstractionConstants.Transport.Headers.TraceParent).Value;

    private static IReadOnlyCollection<KeyValuePair<string, string?>> RemoveTraceParent(IEnumerable<KeyValuePair<string, string?>> headers)
        => [.. headers.Where(static kvp => !string.Equals(
            kvp.Key,
            NOFAbstractionConstants.Transport.Headers.TraceParent,
            StringComparison.OrdinalIgnoreCase))];

    private static Activity? StartBoundaryActivity(NOFInboxMessage message, string? traceParent)
        => NOFInfrastructureConstants.InboundPipeline.Source.StartActivityWithParent(
            $"InboundTransport: {message.PayloadType}",
            ActivityKind.Consumer,
            traceParent);
}
