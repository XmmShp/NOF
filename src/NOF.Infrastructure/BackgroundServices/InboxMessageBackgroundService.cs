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
        List<NOFInboxMessage> pendingMessages;
        using (var claimScope = _serviceProvider.CreateScope())
        {
            claimScope.ServiceProvider.ResolveDaemonServices();
            var dbContext = claimScope.ServiceProvider.GetService<IDbContext>();
            if (dbContext is null)
            {
                _logger.LogDebug("Skipping inbox processing because no IDbContext provider is registered.");
                return;
            }

            pendingMessages = await AtomicClaimPendingMessagesAsync(dbContext, _options.BatchSize, _options.ClaimTimeout, cancellationToken)
                .ToListAsync(cancellationToken);
        }

        if (pendingMessages.Count == 0)
        {
            return;
        }

        var succeededCount = 0;
        var failedCount = 0;

        foreach (var message in pendingMessages.Where(static message => message.OrderKey is null))
        {
            try
            {
                await ProcessSingleMessageAsync(message, cancellationToken);
                succeededCount++;
            }
            catch (Exception)
            {
                failedCount++;
                // Failure details are recorded in the inbox row (retry/failed) and logged at the decision point.
            }
        }

        foreach (var orderedGroup in pendingMessages
                     .Where(static message => message.OrderKey is not null)
                     .GroupBy(static message => (message.Route, message.OrderKey)))
        {
            var orderedMessages = orderedGroup.OrderBy(static message => message.Sequence).ToArray();
            var groupFailed = false;
            for (var index = 0; index < orderedMessages.Length; index++)
            {
                var message = orderedMessages[index];
                try
                {
                    await ProcessSingleMessageAsync(message, cancellationToken);
                    succeededCount++;
                }
                catch (Exception)
                {
                    failedCount++;
                    groupFailed = true;
                    await ReleaseUnprocessedOrderedMessagesAsync(
                        orderedMessages.Skip(index + 1),
                        cancellationToken);
                    break;
                }
            }

            await ReleaseOrderStateClaimAsync(
                orderedGroup.Key.Route,
                orderedGroup.Key.OrderKey!,
                orderedMessages[0].ClaimedBy,
                cancellationToken);

            if (groupFailed)
            {
                continue;
            }
        }

        _logger.LogInformation(
            "Inbox batch processed: {Succeeded} processed, {Failed} failed",
            succeededCount,
            failedCount);
    }

    private async Task ProcessSingleMessageAsync(
        NOFInboxMessage message,
        CancellationToken cancellationToken)
    {
        if (message.Status != InboxMessageStatus.Pending)
        {
            return;
        }

        if (message.RetryCount > _options.MaxRetryCount)
        {
            await MarkFailedAsync(message, "Exceeded max retry count", cancellationToken);
            throw new InvalidOperationException($"Inbox message '{message.Id}' exceeded the maximum retry count.");
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<IDbContext>();
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);

        try
        {
            var pendingMessage = await dbContext.Set<NOFInboxMessage>()
                .FirstOrDefaultAsync(
                    m => m.Id == message.Id && m.Route == message.Route && m.Status == InboxMessageStatus.Pending,
                    cancellationToken);

            if (pendingMessage is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            var orderState = await LoadAndValidateOrderStateAsync(dbContext, pendingMessage, cancellationToken);

            var headers = DeserializeHeaders(pendingMessage.Headers);
            var traceParent = ExtractTraceParent(headers);
            var processingHeaders = RemoveTraceParent(headers);
            var handlerTypeName = ResolveHandlerTypeName(pendingMessage);

            using var activity = StartBoundaryActivity(message, traceParent);

            switch (pendingMessage.MessageType)
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
                    throw new InvalidOperationException($"Unsupported inbox message type '{pendingMessage.MessageType}'.");
            }

            pendingMessage.Status = InboxMessageStatus.Processed;
            pendingMessage.ProcessedAtUtc = DateTime.UtcNow;
            pendingMessage.ErrorMessage = null;
            pendingMessage.ClaimedBy = null;
            pendingMessage.ClaimExpiresAtUtc = null;

            if (orderState is not null)
            {
                orderState.NextSequence = checked(orderState.NextSequence + 1);
                orderState.UpdatedAtUtc = DateTime.UtcNow;
                orderState.ErrorMessage = null;
                if (pendingMessage.CompletesOrderKey)
                {
                    orderState.CompletedAtUtc = DateTime.UtcNow;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            if (message.RetryCount >= _options.MaxRetryCount)
            {
                await MarkFailedAsync(message, ex.Message, cancellationToken, ex);
            }
            else
            {
                await MarkRetryAsync(message, ex.Message, cancellationToken);
            }

            throw;
        }
    }

    private static async Task<NOFInboxOrderState?> LoadAndValidateOrderStateAsync(
        IDbContext dbContext,
        NOFInboxMessage message,
        CancellationToken cancellationToken)
    {
        if (message.OrderKey is null && message.Sequence is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(message.OrderKey) || message.Sequence is null or <= 0)
        {
            throw new InvalidOperationException(
                $"Inbox message '{message.Id}' must have both a non-empty order key and a positive sequence, or neither.");
        }

        var orderState = await dbContext.Set<NOFInboxOrderState>()
            .FirstOrDefaultAsync(
                state => state.Route == message.Route && state.OrderKey == message.OrderKey,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Inbox order state for route '{message.Route}' and key '{message.OrderKey}' does not exist.");

        if (orderState.CompletedAtUtc is not null)
        {
            throw new InvalidOperationException($"Inbox order key '{message.OrderKey}' has already been completed.");
        }

        if (orderState.BlockedSequence is not null)
        {
            throw new InvalidOperationException(
                $"Inbox order key '{message.OrderKey}' is blocked at sequence {orderState.BlockedSequence}.");
        }

        if (!string.Equals(orderState.ClaimedBy, message.ClaimedBy, StringComparison.Ordinal) ||
            orderState.NextSequence != message.Sequence)
        {
            throw new InvalidOperationException(
                $"Inbox message '{message.Id}' sequence {message.Sequence} is not the claimed next sequence {orderState.NextSequence} for key '{message.OrderKey}'.");
        }

        return orderState;
    }

    private async Task MarkRetryAsync(NOFInboxMessage message, string error, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();

        await dbContext.Set<NOFInboxMessage>()
            .Where(m => m.Id == message.Id && m.Route == message.Route && m.Status == InboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.ErrorMessage, error)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAtUtc, (DateTime?)null),
                cancellationToken);

        await ReleaseOrderStateClaimAsync(dbContext, message, error, blocked: false, cancellationToken);

        _logger.LogWarning(
            "Inbox message {InboxId} scheduled for retry #{RetryCount}. Error: {Error}",
            message.Id,
            message.RetryCount,
            error);
    }

    private async Task MarkFailedAsync(
        NOFInboxMessage message,
        string error,
        CancellationToken cancellationToken,
        Exception? ex = null)
    {
        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var failedAt = DateTime.UtcNow;

        await dbContext.Set<NOFInboxMessage>()
            .Where(m => m.Id == message.Id && m.Route == message.Route && m.Status == InboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, InboxMessageStatus.Failed)
                .SetProperty(m => m.ErrorMessage, error)
                .SetProperty(m => m.FailedAtUtc, failedAt)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.ClaimExpiresAtUtc, (DateTime?)null),
                cancellationToken);

        await ReleaseOrderStateClaimAsync(dbContext, message, error, blocked: true, cancellationToken);

        if (ex is null)
        {
            _logger.LogError(
                "Inbox message {InboxId} marked as permanently failed after {RetryCount} retries. Error: {Error}",
                message.Id,
                message.RetryCount,
                error);
        }
        else
        {
            _logger.LogError(
                ex,
                "Inbox message {InboxId} marked as permanently failed after {RetryCount} retries. Error: {Error}",
                message.Id,
                message.RetryCount,
                error);
        }
    }

    private async Task ReleaseUnprocessedOrderedMessagesAsync(
        IEnumerable<NOFInboxMessage> messages,
        CancellationToken cancellationToken)
    {
        foreach (var group in messages.GroupBy(static message => (message.Route, message.OrderKey)))
        {
            var claimedBy = group.Select(static message => message.ClaimedBy).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(claimedBy))
            {
                continue;
            }

            var sequences = group
                .Where(static message => message.Sequence.HasValue)
                .Select(static message => message.Sequence!.Value)
                .ToArray();
            if (sequences.Length == 0)
            {
                continue;
            }

            using var scope = _serviceProvider.CreateScope();
            scope.ServiceProvider.ResolveDaemonServices();
            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
            await dbContext.Set<NOFInboxMessage>()
                .Where(message => message.Route == group.Key.Route &&
                                  message.OrderKey == group.Key.OrderKey &&
                                  message.Sequence != null &&
                                  sequences.Contains(message.Sequence.Value) &&
                                  message.Status == InboxMessageStatus.Pending &&
                                  message.ClaimedBy == claimedBy)
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(message => message.RetryCount, message => message.RetryCount - 1)
                        .SetProperty(message => message.ClaimedBy, (string?)null)
                        .SetProperty(message => message.ClaimExpiresAtUtc, (DateTime?)null),
                    cancellationToken);
        }
    }

    private async Task ReleaseOrderStateClaimAsync(
        string route,
        string orderKey,
        string? claimedBy,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimedBy))
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.ResolveDaemonServices();
        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        await ReleaseOrderStateClaimAsync(dbContext, route, orderKey, claimedBy, cancellationToken);
    }

    private static async Task ReleaseOrderStateClaimAsync(
        IDbContext dbContext,
        string route,
        string orderKey,
        string claimedBy,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<NOFInboxOrderState>()
            .Where(state => state.Route == route &&
                            state.OrderKey == orderKey &&
                            state.ClaimedBy == claimedBy)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(state => state.ClaimedBy, (string?)null)
                    .SetProperty(state => state.ClaimExpiresAtUtc, (DateTime?)null),
                cancellationToken);
    }

    private static async Task ReleaseOrderStateClaimAsync(
        IDbContext dbContext,
        NOFInboxMessage message,
        string error,
        bool blocked,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.OrderKey))
        {
            return;
        }

        var states = dbContext.Set<NOFInboxOrderState>()
            .Where(state => state.Route == message.Route && state.OrderKey == message.OrderKey);
        var updatedAtUtc = DateTime.UtcNow;
        if (blocked)
        {
            await states.ExecuteUpdateAsync(setters => setters
                    .SetProperty(state => state.ClaimedBy, (string?)null)
                    .SetProperty(state => state.ClaimExpiresAtUtc, (DateTime?)null)
                    .SetProperty(state => state.UpdatedAtUtc, updatedAtUtc)
                    .SetProperty(state => state.ErrorMessage, error)
                    .SetProperty(state => state.BlockedSequence, message.Sequence),
                cancellationToken);
            return;
        }

        await states.ExecuteUpdateAsync(setters => setters
                .SetProperty(state => state.ClaimedBy, (string?)null)
                .SetProperty(state => state.ClaimExpiresAtUtc, (DateTime?)null)
                .SetProperty(state => state.UpdatedAtUtc, updatedAtUtc)
                .SetProperty(state => state.ErrorMessage, error),
            cancellationToken);
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

        await dbContext.Set<NOFInboxOrderState>()
            .Where(state => state.CompletedAtUtc == null &&
                            state.BlockedSequence == null &&
                            dbContext.Set<NOFInboxMessage>().Any(message =>
                                message.Route == state.Route &&
                                message.OrderKey == state.OrderKey &&
                                message.Sequence == state.NextSequence &&
                                message.Status == InboxMessageStatus.Failed))
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(state => state.BlockedSequence, state => state.NextSequence)
                    .SetProperty(state => state.ErrorMessage, "The next ordered inbox message failed permanently.")
                    .SetProperty(state => state.UpdatedAtUtc, now)
                    .SetProperty(state => state.ClaimedBy, (string?)null)
                    .SetProperty(state => state.ClaimExpiresAtUtc, (DateTime?)null),
                cancellationToken);

        var claimedCount = 0;

        var candidateStates = await dbContext.Set<NOFInboxOrderState>()
            .AsNoTracking()
            .Join(
                dbContext.Set<NOFInboxMessage>()
                    .AsNoTracking()
                    .Where(message => message.Status == InboxMessageStatus.Pending &&
                                      message.RetryCount < _options.MaxRetryCount &&
                                      (message.ClaimedBy == null ||
                                       message.ClaimExpiresAtUtc == null ||
                                       message.ClaimExpiresAtUtc <= now)),
                state => new { state.Route, state.OrderKey, Sequence = (long?)state.NextSequence },
                message => new { message.Route, OrderKey = message.OrderKey!, message.Sequence },
                (state, _) => state)
            .Where(state => state.CompletedAtUtc == null &&
                            state.BlockedSequence == null &&
                            (state.ClaimedBy == null || state.ClaimExpiresAtUtc == null || state.ClaimExpiresAtUtc <= now))
            .OrderBy(state => state.UpdatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        var claimedStates = new List<NOFInboxOrderState>(candidateStates.Count);
        foreach (var state in candidateStates)
        {
            var nextMessageExists = await dbContext.Set<NOFInboxMessage>()
                .AsNoTracking()
                .Where(message => message.Route == state.Route &&
                                  message.OrderKey == state.OrderKey &&
                                  message.Sequence == state.NextSequence &&
                                  message.Status == InboxMessageStatus.Pending &&
                                  message.RetryCount < _options.MaxRetryCount &&
                                  (message.ClaimedBy == null || message.ClaimExpiresAtUtc == null || message.ClaimExpiresAtUtc <= now))
                .AnyAsync(cancellationToken);
            if (!nextMessageExists)
            {
                continue;
            }

            var stateClaimed = await dbContext.Set<NOFInboxOrderState>()
                .Where(candidate => candidate.Route == state.Route &&
                                    candidate.OrderKey == state.OrderKey &&
                                    candidate.NextSequence == state.NextSequence &&
                                    candidate.CompletedAtUtc == null &&
                                    candidate.BlockedSequence == null &&
                                    (candidate.ClaimedBy == null ||
                                     candidate.ClaimExpiresAtUtc == null ||
                                     candidate.ClaimExpiresAtUtc <= now))
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(candidate => candidate.ClaimedBy, lockId)
                        .SetProperty(candidate => candidate.ClaimExpiresAtUtc, expiresAt),
                    cancellationToken);
            if (stateClaimed > 0)
            {
                claimedStates.Add(state);
            }
        }

        var claimedStateKeysWithMessages = new HashSet<(string Route, string OrderKey)>();

        foreach (var state in claimedStates)
        {
            if (claimedCount >= batchSize)
            {
                break;
            }

            var candidates = await dbContext.Set<NOFInboxMessage>()
                .AsNoTracking()
                .Where(message => message.Route == state.Route &&
                                  message.OrderKey == state.OrderKey &&
                                  message.Sequence >= state.NextSequence &&
                                  message.Status == InboxMessageStatus.Pending &&
                                  message.RetryCount < _options.MaxRetryCount &&
                                  (message.ClaimedBy == null || message.ClaimExpiresAtUtc == null || message.ClaimExpiresAtUtc <= now))
                .OrderBy(message => message.Sequence)
                .Take(batchSize - claimedCount)
                .ToListAsync(cancellationToken);

            var contiguousSequences = new List<long>(candidates.Count);
            var expectedSequence = state.NextSequence;
            foreach (var candidate in candidates)
            {
                if (candidate.Sequence != expectedSequence)
                {
                    break;
                }

                contiguousSequences.Add(expectedSequence);
                expectedSequence++;
            }

            if (contiguousSequences.Count == 0)
            {
                await ReleaseOrderStateClaimAsync(
                    dbContext,
                    state.Route,
                    state.OrderKey,
                    lockId,
                    cancellationToken);
                continue;
            }

            var claimedForState = await dbContext.Set<NOFInboxMessage>()
                .Where(message => message.Route == state.Route &&
                                  message.OrderKey == state.OrderKey &&
                                  message.Sequence != null &&
                                  contiguousSequences.Contains(message.Sequence.Value) &&
                                  message.Status == InboxMessageStatus.Pending &&
                                  (message.ClaimedBy == null || message.ClaimExpiresAtUtc == null || message.ClaimExpiresAtUtc <= now))
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(message => message.RetryCount, message => message.RetryCount + 1)
                        .SetProperty(message => message.ClaimedBy, lockId)
                        .SetProperty(message => message.ClaimExpiresAtUtc, expiresAt),
                    cancellationToken);
            if (claimedForState > 0)
            {
                claimedCount += claimedForState;
                claimedStateKeysWithMessages.Add((state.Route, state.OrderKey));
            }
        }

        foreach (var state in claimedStates.Where(state =>
                     !claimedStateKeysWithMessages.Contains((state.Route, state.OrderKey))))
        {
            await ReleaseOrderStateClaimAsync(
                dbContext,
                state.Route,
                state.OrderKey,
                lockId,
                cancellationToken);
        }

        if (claimedCount < batchSize)
        {
            claimedCount += await dbContext.Set<NOFInboxMessage>()
                .Where(m => m.Status == InboxMessageStatus.Pending &&
                            m.OrderKey == null &&
                            m.Sequence == null &&
                            m.RetryCount < _options.MaxRetryCount &&
                            (m.ClaimedBy == null || m.ClaimExpiresAtUtc == null || m.ClaimExpiresAtUtc <= now))
                .OrderBy(m => m.CreatedAtUtc)
                .ThenBy(m => m.Id)
                .Take(batchSize - claimedCount)
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(m => m.RetryCount, m => m.RetryCount + 1)
                        .SetProperty(m => m.ClaimedBy, lockId)
                        .SetProperty(m => m.ClaimExpiresAtUtc, expiresAt),
                    cancellationToken);
        }

        if (claimedCount == 0)
        {
            foreach (var state in claimedStates)
            {
                await ReleaseOrderStateClaimAsync(
                    dbContext,
                    state.Route,
                    state.OrderKey,
                    lockId,
                    cancellationToken);
            }

            yield break;
        }

        var claimed = await dbContext.Set<NOFInboxMessage>()
            .AsNoTracking()
            .Where(m => m.ClaimedBy == lockId)
            .OrderBy(m => m.OrderKey == null ? 1 : 0)
            .ThenBy(m => m.Route)
            .ThenBy(m => m.OrderKey)
            .ThenBy(m => m.Sequence)
            .ThenBy(m => m.CreatedAtUtc)
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
