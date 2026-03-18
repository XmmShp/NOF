using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Domain;
using NOF.Infrastructure.Abstraction;
using System.Collections.Concurrent;

namespace NOF.Infrastructure.Core;

public abstract class InMemoryRepository<TAggregateRoot, TKey> : IRepository<TAggregateRoot, TKey>
    where TAggregateRoot : class, IAggregateRoot
    where TKey : notnull
{
    private readonly Func<string> _partitionNameFactory;
    private readonly Func<TAggregateRoot, TKey> _keySelector;
    private readonly Func<TAggregateRoot, TAggregateRoot> _cloner;
    private readonly IEqualityComparer<TKey>? _keyComparer;

    protected InMemoryRepository(
        InMemoryPersistenceStore store,
        InMemoryPersistenceSession session,
        string partitionName,
        Func<TAggregateRoot, TKey> keySelector,
        Func<TAggregateRoot, TAggregateRoot> cloner,
        IEqualityComparer<TKey>? keyComparer = null)
        : this(store, session, () => partitionName, keySelector, cloner, keyComparer)
    {
    }

    protected InMemoryRepository(
        InMemoryPersistenceStore store,
        InMemoryPersistenceSession session,
        Func<string> partitionNameFactory,
        Func<TAggregateRoot, TKey> keySelector,
        Func<TAggregateRoot, TAggregateRoot> cloner,
        IEqualityComparer<TKey>? keyComparer = null)
    {
        Store = store;
        Session = session;
        _partitionNameFactory = partitionNameFactory;
        _keySelector = keySelector;
        _cloner = cloner;
        _keyComparer = keyComparer;
    }

    protected InMemoryPersistenceStore Store { get; }

    protected InMemoryPersistenceSession Session { get; }

    protected ConcurrentDictionary<TKey, TAggregateRoot> Items
        => Store.GetPartition(_partitionNameFactory(), _keySelector, _cloner, _keyComparer).Items;

    protected virtual IEnumerable<TAggregateRoot> OrderItems(IEnumerable<TAggregateRoot> items)
        => items;

    public virtual ValueTask<TAggregateRoot?> FindAsync(object?[] keyValues, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (keyValues is not [TKey key])
        {
            return ValueTask.FromResult<TAggregateRoot?>(null);
        }

        var entity = Items.TryGetValue(key, out var found) ? Track(found) : null;
        return ValueTask.FromResult(entity);
    }

    public virtual async IAsyncEnumerable<TAggregateRoot> FindAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var entity in OrderItems(Items.Values))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Track(entity);
            await Task.CompletedTask;
        }
    }

    public virtual void Add(TAggregateRoot entity)
    {
        Items[_keySelector(entity)] = entity;
        Session.RegisterChange(entity);
    }

    public virtual void Remove(TAggregateRoot entity)
    {
        Items.TryRemove(_keySelector(entity), out _);
        Session.RegisterChange(entity);
    }

    protected TAggregateRoot Track(TAggregateRoot aggregateRoot)
    {
        Session.Track(aggregateRoot);
        return aggregateRoot;
    }
}

internal sealed class InMemoryInboxMessageRepository : InMemoryRepository<NOFInboxMessage, Guid>, IInboxMessageRepository
{
    public InMemoryInboxMessageRepository(InMemoryPersistenceStore store, InMemoryPersistenceSession session)
        : base(store, session, "nof:inbox", static message => message.Id, static message => new NOFInboxMessage(message.Id)
        {
            CreatedAt = message.CreatedAt
        })
    {
    }

    public Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Items.ContainsKey(messageId));
    }

    protected override IEnumerable<NOFInboxMessage> OrderItems(IEnumerable<NOFInboxMessage> items)
        => items.OrderBy(message => message.CreatedAt);
}

internal sealed class InMemoryTenantRepository : InMemoryRepository<NOFTenant, string>, ITenantRepository
{
    private readonly ILogger<InMemoryTenantRepository> _logger;

    public InMemoryTenantRepository(InMemoryPersistenceStore store, InMemoryPersistenceSession session, ILogger<InMemoryTenantRepository> logger)
        : base(store, session, "nof:tenant", static tenant => tenant.Id, static tenant => new NOFTenant
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Description = tenant.Description,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt
        }, StringComparer.OrdinalIgnoreCase)
    {
        _logger = logger;
    }

    public ValueTask<bool> ExistsAsync(string tenantId)
    {
        var exists = !string.IsNullOrWhiteSpace(tenantId) && Items.ContainsKey(tenantId);
        _logger.LogDebug("Checked existence of tenant {TenantId}: {Exists}", tenantId, exists);
        return ValueTask.FromResult(exists);
    }

    protected override IEnumerable<NOFTenant> OrderItems(IEnumerable<NOFTenant> items)
        => items.OrderBy(tenant => tenant.Id, StringComparer.OrdinalIgnoreCase);
}

internal sealed class InMemoryStateMachineContextRepository : InMemoryRepository<NOFStateMachineContext, string>, IStateMachineContextRepository
{
    public InMemoryStateMachineContextRepository(InMemoryPersistenceStore store, InMemoryPersistenceSession session, IInvocationContext invocationContext)
        : base(
            store,
            session,
            () => $"nof:state-machine:{InMemoryPersistenceStore.NormalizeTenantId(invocationContext.TenantId)}",
            static context => BuildKey(context.CorrelationId, context.DefinitionTypeName),
            static context => new NOFStateMachineContext
            {
                CorrelationId = context.CorrelationId,
                DefinitionTypeName = context.DefinitionTypeName,
                State = context.State
            },
            StringComparer.OrdinalIgnoreCase)
    {
    }

    public override ValueTask<NOFStateMachineContext?> FindAsync(object?[] keyValues, CancellationToken cancellationToken = default)
    {
        if (keyValues is not [string correlationId, string definitionTypeName])
        {
            return ValueTask.FromResult<NOFStateMachineContext?>(null);
        }

        return base.FindAsync([BuildKey(correlationId, definitionTypeName)], cancellationToken);
    }

    protected override IEnumerable<NOFStateMachineContext> OrderItems(IEnumerable<NOFStateMachineContext> items)
        => items.OrderBy(context => context.CorrelationId, StringComparer.OrdinalIgnoreCase);

    private static string BuildKey(string correlationId, string definitionTypeName)
        => $"{correlationId}\u001f{definitionTypeName}";
}

internal sealed class InMemoryOutboxMessageRepository : InMemoryRepository<NOFOutboxMessage, long>, IOutboxMessageRepository
{
    private readonly IOptions<OutboxOptions> _options;
    private readonly ILogger<InMemoryOutboxMessageRepository> _logger;
    private readonly IIdGenerator _idGenerator;

    public InMemoryOutboxMessageRepository(
        InMemoryPersistenceStore store,
        InMemoryPersistenceSession session,
        IInvocationContext invocationContext,
        IOptions<OutboxOptions> options,
        ILogger<InMemoryOutboxMessageRepository> logger,
        IIdGenerator idGenerator)
        : base(
            store,
            session,
            () => $"nof:outbox:{InMemoryPersistenceStore.NormalizeTenantId(invocationContext.TenantId)}",
            static message => message.Id,
            static message => new NOFOutboxMessage
            {
                Id = message.Id,
                DestinationEndpointName = message.DestinationEndpointName,
                CreatedAt = message.CreatedAt,
                RetryCount = message.RetryCount,
                MessageType = message.MessageType,
                PayloadType = message.PayloadType,
                Payload = message.Payload,
                Headers = message.Headers,
                SentAt = message.SentAt,
                FailedAt = message.FailedAt,
                ErrorMessage = message.ErrorMessage,
                ClaimedBy = message.ClaimedBy,
                ClaimExpiresAt = message.ClaimExpiresAt,
                Status = message.Status,
                TraceId = message.TraceId,
                SpanId = message.SpanId
            })
    {
        _options = options;
        _logger = logger;
        _idGenerator = idGenerator;
    }

    public override void Add(NOFOutboxMessage entity)
    {
        if (entity.Id == 0)
        {
            entity.Id = _idGenerator.NextId();
        }

        entity.Status = OutboxMessageStatus.Pending;
        entity.ClaimedBy = null;
        entity.ClaimExpiresAt = null;
        base.Add(entity);
    }

    public async IAsyncEnumerable<NOFOutboxMessage> AtomicClaimPendingMessagesAsync(int batchSize = 100, TimeSpan? claimTimeout = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var timeout = claimTimeout ?? _options.Value.ClaimTimeout;
        var claimedBy = Guid.NewGuid().ToString("N");
        var expiresAt = DateTime.UtcNow.Add(timeout);
        List<NOFOutboxMessage> claimed;

        lock (Store.SyncRoot)
        {
            claimed = Items.Values
                .Where(m => m.Status == OutboxMessageStatus.Pending &&
                            m.RetryCount < _options.Value.MaxRetryCount &&
                            (m.ClaimExpiresAt is null || m.ClaimExpiresAt <= DateTime.UtcNow))
                .OrderBy(m => m.CreatedAt)
                .Take(batchSize)
                .Select(message =>
                {
                    message.RetryCount++;
                    message.ClaimedBy = claimedBy;
                    message.ClaimExpiresAt = expiresAt;
                    return Store.CloneEntity(message);
                })
                .ToList();
        }

        foreach (var message in claimed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return message;
            await Task.CompletedTask;
        }
    }

    public ValueTask AtomicMarkAsSentAsync(IEnumerable<long> messageIds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (Store.SyncRoot)
        {
            foreach (var messageId in messageIds)
            {
                if (!Items.TryGetValue(messageId, out var message) || message.Status != OutboxMessageStatus.Pending)
                {
                    continue;
                }

                message.Status = OutboxMessageStatus.Sent;
                message.SentAt = DateTime.UtcNow;
                message.ClaimedBy = null;
                message.ClaimExpiresAt = null;
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask AtomicRecordDeliveryFailureAsync(long messageId, string errorMessage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (Store.SyncRoot)
        {
            if (!Items.TryGetValue(messageId, out var message) || message.Status != OutboxMessageStatus.Pending)
            {
                _logger.LogDebug("Message {MessageId} already processed or not in pending state", messageId);
                return ValueTask.CompletedTask;
            }

            message.ErrorMessage = errorMessage;
            message.FailedAt = DateTime.UtcNow;

            if (message.RetryCount >= _options.Value.MaxRetryCount)
            {
                message.Status = OutboxMessageStatus.Failed;
                message.ClaimedBy = null;
                message.ClaimExpiresAt = null;
                _logger.LogWarning("Message {MessageId} marked as permanently failed after {RetryCount} retries. Error: {Error}", messageId, message.RetryCount, errorMessage);
            }
            else
            {
                message.Status = OutboxMessageStatus.Pending;
                message.ClaimedBy = null;
                message.ClaimExpiresAt = null;
                _logger.LogWarning("Message {MessageId} scheduled for retry #{RetryCount}. Error: {Error}", messageId, message.RetryCount, errorMessage);
            }
        }

        return ValueTask.CompletedTask;
    }

    protected override IEnumerable<NOFOutboxMessage> OrderItems(IEnumerable<NOFOutboxMessage> items)
        => items.OrderBy(message => message.CreatedAt);
}
