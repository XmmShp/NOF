using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Application;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Infrastructure;

public sealed class InboxMessageBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TransactionalMessageProcessorOptions _options;
    private readonly ILogger<InboxMessageBackgroundService> _logger;
    private readonly CommandInboundPipelineExecutor _commandPipelineExecutor;
    private readonly NotificationInboundPipelineExecutor _notificationPipelineExecutor;

    public InboxMessageBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<TransactionalMessageOptions> options,
        ILogger<InboxMessageBackgroundService> logger,
        CommandInboundPipelineExecutor commandPipelineExecutor,
        NotificationInboundPipelineExecutor notificationPipelineExecutor)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value.Inbox;
        _logger = logger;
        _commandPipelineExecutor = commandPipelineExecutor;
        _notificationPipelineExecutor = notificationPipelineExecutor;
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
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

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
                await ProcessSingleMessageAsync(message, cancellationToken);
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

    private async Task ProcessSingleMessageAsync(NOFInboxMessage message, CancellationToken cancellationToken)
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
        var handlerType = TypeRegistry.Resolve(message.HandlerType);

        try
        {
            switch (message.MessageType)
            {
                case InboxMessageType.Command:
                    {
                        await _commandPipelineExecutor.ExecuteAsync(
                            message.Payload,
                            message.PayloadType,
                            handlerType,
                            headers,
                            cancellationToken);
                        break;
                    }
                case InboxMessageType.Notification:
                    {
                        await _notificationPipelineExecutor.ExecuteAsync(
                            message.Payload,
                            message.PayloadType,
                            handlerType,
                            headers,
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
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
        var processedAt = DateTime.UtcNow;

        await dbContext.Set<NOFInboxMessage>()
            .Where(m => m.Id == messageId && m.HandlerType == handlerType && m.Status == InboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, InboxMessageStatus.Processed)
                .SetProperty(m => m.ProcessedAt, processedAt)
                .SetProperty(m => m.ErrorMessage, (string?)null)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAt, (DateTime?)null),
                cancellationToken);
    }

    private async Task MarkRetryAsync(Guid messageId, string handlerType, int retryCount, string error, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

        await dbContext.Set<NOFInboxMessage>()
            .Where(m => m.Id == messageId && m.HandlerType == handlerType && m.Status == InboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.ErrorMessage, error)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAt, (DateTime?)null),
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
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
        var failedAt = DateTime.UtcNow;

        await dbContext.Set<NOFInboxMessage>()
            .Where(m => m.Id == messageId && m.HandlerType == handlerType && m.Status == InboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, InboxMessageStatus.Failed)
                .SetProperty(m => m.ErrorMessage, error)
                .SetProperty(m => m.FailedAt, failedAt)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAt, (DateTime?)null),
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
        DbContext dbContext,
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

        var rowsUpdated = await dbContext.Set<NOFInboxMessage>()
            .Where(m => m.Status == InboxMessageStatus.Pending &&
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

        var claimed = await dbContext.Set<NOFInboxMessage>()
            .AsNoTracking()
            .Where(m => m.ClaimedBy == lockId)
            .ToListAsync(cancellationToken);

        foreach (var msgFromDb in claimed)
        {
            yield return msgFromDb;
        }
    }

    private static IEnumerable<KeyValuePair<string, string?>> DeserializeHeaders(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<KeyValuePair<string, string?>>();
        }

        var headersTypeInfo = (JsonTypeInfo<Dictionary<string, string?>>)JsonSerializerOptions.NOF.GetTypeInfo(typeof(Dictionary<string, string?>));
        var dict = JsonSerializer.Deserialize(raw, headersTypeInfo) ?? new Dictionary<string, string?>();
        return dict;
    }
}
