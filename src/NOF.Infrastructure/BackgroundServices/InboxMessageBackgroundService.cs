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
    private readonly CommandHandlerRegistry _commandHandlerRegistry;
    private readonly NotificationHandlerRegistry _notificationHandlerRegistry;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly TransactionalMessageProcessorOptions _options;
    private readonly ILogger<InboxMessageBackgroundService> _logger;
    private readonly IObjectSerializer _objectSerializer;

    public InboxMessageBackgroundService(
        IServiceProvider serviceProvider,
        CommandHandlerRegistry commandHandlerRegistry,
        NotificationHandlerRegistry notificationHandlerRegistry,
        IHostEnvironment hostEnvironment,
        IOptions<TransactionalMessageOptions> options,
        ILogger<InboxMessageBackgroundService> logger,
        IObjectSerializer objectSerializer)
    {
        _serviceProvider = serviceProvider;
        _commandHandlerRegistry = commandHandlerRegistry;
        _notificationHandlerRegistry = notificationHandlerRegistry;
        _hostEnvironment = hostEnvironment;
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
            await MarkFailedAsync(message.Id, message.Route, message.RetryCount, "Exceeded max retry count", cancellationToken);
            return;
        }

        var headers = DeserializeHeaders(message.Headers);
        var traceParent = ExtractTraceParent(headers);
        var processingHeaders = RemoveTraceParent(headers);
        var handlerTypeName = ResolveHandlerTypeName(message);

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
                            handlerTypeName,
                            processingHeaders,
                            cancellationToken);
                        break;
                    }
                case InboxMessageType.Notification:
                    {
                        var notificationPipelineExecutor = services.GetRequiredService<NotificationInboundPipelineExecutor>();
                        await notificationPipelineExecutor.ExecuteAsync(
                            message.Payload,
                            handlerTypeName,
                            processingHeaders,
                            cancellationToken);
                        break;
                    }
                default:
                    throw new InvalidOperationException($"Unsupported inbox message type '{message.MessageType}'.");
            }

            await MarkProcessedAsync(message.Id, message.Route, cancellationToken);
        }
        catch (Exception ex)
        {
            if (message.RetryCount >= _options.MaxRetryCount)
            {
                await MarkFailedAsync(message.Id, message.Route, message.RetryCount, ex.Message, cancellationToken, ex);
            }
            else
            {
                await MarkRetryAsync(message.Id, message.Route, message.RetryCount, ex.Message, cancellationToken);
            }

            throw;
        }
    }

    private async Task MarkProcessedAsync(Guid messageId, string route, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var processedAt = DateTime.UtcNow;

        await dbContext.Set<NOFInboxMessage>()
            .Where(m => m.Id == messageId && m.Route == route && m.Status == InboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, InboxMessageStatus.Processed)
                .SetProperty(m => m.ProcessedAtUtc, processedAt)
                .SetProperty(m => m.ErrorMessage, (string?)null)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAtUtc, (DateTime?)null),
                cancellationToken);
    }

    private async Task MarkRetryAsync(Guid messageId, string route, int retryCount, string error, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();

        await dbContext.Set<NOFInboxMessage>()
            .Where(m => m.Id == messageId && m.Route == route && m.Status == InboxMessageStatus.Pending)
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

    private async Task MarkFailedAsync(Guid messageId, string route, int retryCount, string error, CancellationToken cancellationToken, Exception? ex = null)
    {
        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var failedAt = DateTime.UtcNow;

        await dbContext.Set<NOFInboxMessage>()
            .Where(m => m.Id == messageId && m.Route == route && m.Status == InboxMessageStatus.Pending)
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
            $"InboundTransport: {message.Route}",
            ActivityKind.Consumer,
            traceParent);

    private string ResolveHandlerTypeName(NOFInboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Route);

        return message.MessageType switch
        {
            InboxMessageType.Command => ResolveCommandHandlerTypeName(message.Route),
            InboxMessageType.Notification => ResolveNotificationHandlerTypeName(message.Route),
            _ => throw new InvalidOperationException($"Unsupported inbox message type '{message.MessageType}'.")
        };
    }

    private string ResolveCommandHandlerTypeName(string route)
    {
        if (_commandHandlerRegistry.TryGetHandlerType(route, out var handlerType))
        {
            return handlerType.DisplayName;
        }

        var resolvedHandlerType = _commandHandlerRegistry.GetHandlers(route).FirstOrDefault()
            ?? throw new InvalidOperationException($"No command handler route is registered for '{route}'.");

        return resolvedHandlerType.DisplayName;
    }

    private string ResolveNotificationHandlerTypeName(string route)
    {
        if (_notificationHandlerRegistry.TryGetHandlerType(route, out var handlerType))
        {
            return handlerType.DisplayName;
        }

        foreach (var notificationGroup in _notificationHandlerRegistry.Freeze().GroupBy(static registration => registration.HandlerType))
        {
            var handlerRoute = BuildNotificationRoute(_hostEnvironment.ServiceName, notificationGroup.Key.DisplayName);

            if (string.Equals(handlerRoute, route, StringComparison.Ordinal))
            {
                return notificationGroup.Key.DisplayName;
            }
        }

        throw new InvalidOperationException($"No notification handler route is registered for '{route}'.");
    }

    private static string BuildNotificationRoute(string? serviceName, string handlerDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerDisplayName);

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return handlerDisplayName;
        }

        return $"{serviceName}.{handlerDisplayName}";
    }
}
