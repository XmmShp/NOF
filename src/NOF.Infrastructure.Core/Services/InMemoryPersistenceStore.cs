using NOF.Application;
using NOF.Infrastructure.Abstraction;
using System.Collections.Concurrent;

namespace NOF.Infrastructure.Core;

public sealed class InMemoryPersistenceStore
{
    public object SyncRoot { get; } = new();

    public ConcurrentDictionary<Guid, NOFInboxMessage> InboxMessages { get; private set; } = new();

    public ConcurrentDictionary<string, NOFTenant> Tenants { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentDictionary<string, ConcurrentDictionary<long, NOFOutboxMessage>> OutboxMessagesByTenant { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentDictionary<string, ConcurrentDictionary<string, NOFStateMachineContext>> StateMachineContextsByTenant { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    private ConcurrentDictionary<string, IInMemoryPersistencePartition> CustomPartitions { get; set; } = new(StringComparer.Ordinal);

    internal InMemoryPersistenceStoreSnapshot CaptureSnapshot()
    {
        lock (SyncRoot)
        {
            return new InMemoryPersistenceStoreSnapshot(
                CloneDictionary(InboxMessages),
                CloneDictionary(Tenants),
                CloneNestedDictionary(OutboxMessagesByTenant),
                CloneNestedDictionary(StateMachineContextsByTenant),
                ClonePartitionDictionary(CustomPartitions));
        }
    }

    internal void RestoreSnapshot(InMemoryPersistenceStoreSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (SyncRoot)
        {
            InboxMessages = CloneDictionary(snapshot.InboxMessages);
            Tenants = CloneDictionary(snapshot.Tenants, StringComparer.OrdinalIgnoreCase);
            OutboxMessagesByTenant = CloneNestedDictionary(snapshot.OutboxMessagesByTenant, StringComparer.OrdinalIgnoreCase);
            StateMachineContextsByTenant = CloneNestedDictionary(snapshot.StateMachineContextsByTenant, StringComparer.OrdinalIgnoreCase);
            CustomPartitions = ClonePartitionDictionary(snapshot.CustomPartitions);
        }
    }

    public ConcurrentDictionary<long, NOFOutboxMessage> GetOutboxPartition(string? tenantId)
    {
        var partitionKey = NormalizeTenantId(tenantId);
        return OutboxMessagesByTenant.GetOrAdd(partitionKey, static _ => new ConcurrentDictionary<long, NOFOutboxMessage>());
    }

    public ConcurrentDictionary<string, NOFStateMachineContext> GetStateMachinePartition(string? tenantId)
    {
        var partitionKey = NormalizeTenantId(tenantId);
        return StateMachineContextsByTenant.GetOrAdd(partitionKey, static _ => new ConcurrentDictionary<string, NOFStateMachineContext>(StringComparer.OrdinalIgnoreCase));
    }

    public static string NormalizeTenantId(string? tenantId)
        => string.IsNullOrWhiteSpace(tenantId) ? string.Empty : tenantId;

    public InMemoryPersistencePartition<TAggregateRoot, TKey> GetPartition<TAggregateRoot, TKey>(
        string partitionName,
        Func<TAggregateRoot, TKey> keySelector,
        Func<TAggregateRoot, TAggregateRoot> cloner,
        IEqualityComparer<TKey>? keyComparer = null)
        where TAggregateRoot : class
        where TKey : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionName);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(cloner);

        var partition = CustomPartitions.GetOrAdd(partitionName, static (_, state) =>
            new InMemoryPersistencePartition<TAggregateRoot, TKey>(state.keySelector, state.cloner, state.keyComparer),
            (keySelector, cloner, keyComparer));

        if (partition is not InMemoryPersistencePartition<TAggregateRoot, TKey> typedPartition)
        {
            throw new InvalidOperationException($"Partition '{partitionName}' is already registered with a different entity or key type.");
        }

        return typedPartition;
    }

    private ConcurrentDictionary<TKey, TValue> CloneDictionary<TKey, TValue>(ConcurrentDictionary<TKey, TValue> source)
        where TKey : notnull
        where TValue : class
        => CloneDictionary(source, comparer: null);

    private ConcurrentDictionary<TKey, TValue> CloneDictionary<TKey, TValue>(ConcurrentDictionary<TKey, TValue> source, IEqualityComparer<TKey>? comparer)
        where TKey : notnull
        where TValue : class
    {
        var clone = comparer is null
            ? new ConcurrentDictionary<TKey, TValue>()
            : new ConcurrentDictionary<TKey, TValue>(comparer);

        foreach (var pair in source)
        {
            clone[pair.Key] = CloneEntity(pair.Value);
        }

        return clone;
    }

    private ConcurrentDictionary<TKeyOuter, ConcurrentDictionary<TKeyInner, TValue>> CloneNestedDictionary<TKeyOuter, TKeyInner, TValue>(
        ConcurrentDictionary<TKeyOuter, ConcurrentDictionary<TKeyInner, TValue>> source,
        IEqualityComparer<TKeyOuter>? comparer = null)
        where TKeyOuter : notnull
        where TKeyInner : notnull
        where TValue : class
    {
        var clone = comparer is null
            ? new ConcurrentDictionary<TKeyOuter, ConcurrentDictionary<TKeyInner, TValue>>()
            : new ConcurrentDictionary<TKeyOuter, ConcurrentDictionary<TKeyInner, TValue>>(comparer);

        foreach (var pair in source)
        {
            clone[pair.Key] = CloneDictionary(pair.Value);
        }

        return clone;
    }

    private ConcurrentDictionary<string, IInMemoryPersistencePartition> ClonePartitionDictionary(
        ConcurrentDictionary<string, IInMemoryPersistencePartition> source)
    {
        var clone = new ConcurrentDictionary<string, IInMemoryPersistencePartition>(StringComparer.Ordinal);
        foreach (var pair in source)
        {
            clone[pair.Key] = pair.Value.Clone();
        }

        return clone;
    }

    public TEntity CloneEntity<TEntity>(TEntity entity)
        where TEntity : class
    {
        if (entity is NOFInboxMessage inboxMessage)
        {
            return (TEntity)(object)new NOFInboxMessage(inboxMessage.Id)
            {
                CreatedAt = inboxMessage.CreatedAt
            };
        }

        if (entity is NOFTenant tenant)
        {
            return (TEntity)(object)new NOFTenant
            {
                Id = tenant.Id,
                Name = tenant.Name,
                Description = tenant.Description,
                IsActive = tenant.IsActive,
                CreatedAt = tenant.CreatedAt,
                UpdatedAt = tenant.UpdatedAt
            };
        }

        if (entity is NOFOutboxMessage outboxMessage)
        {
            return (TEntity)(object)new NOFOutboxMessage
            {
                Id = outboxMessage.Id,
                DestinationEndpointName = outboxMessage.DestinationEndpointName,
                CreatedAt = outboxMessage.CreatedAt,
                RetryCount = outboxMessage.RetryCount,
                MessageType = outboxMessage.MessageType,
                PayloadType = outboxMessage.PayloadType,
                Payload = outboxMessage.Payload,
                Headers = outboxMessage.Headers,
                SentAt = outboxMessage.SentAt,
                FailedAt = outboxMessage.FailedAt,
                ErrorMessage = outboxMessage.ErrorMessage,
                ClaimedBy = outboxMessage.ClaimedBy,
                ClaimExpiresAt = outboxMessage.ClaimExpiresAt,
                Status = outboxMessage.Status,
                TraceId = outboxMessage.TraceId,
                SpanId = outboxMessage.SpanId
            };
        }

        if (entity is NOFStateMachineContext stateMachineContext)
        {
            return (TEntity)(object)new NOFStateMachineContext
            {
                CorrelationId = stateMachineContext.CorrelationId,
                DefinitionTypeName = stateMachineContext.DefinitionTypeName,
                State = stateMachineContext.State
            };
        }

        throw new NotSupportedException($"Cloning is not supported for entity type {typeof(TEntity).FullName}.");
    }
}

internal sealed record InMemoryPersistenceStoreSnapshot(
    ConcurrentDictionary<Guid, NOFInboxMessage> InboxMessages,
    ConcurrentDictionary<string, NOFTenant> Tenants,
    ConcurrentDictionary<string, ConcurrentDictionary<long, NOFOutboxMessage>> OutboxMessagesByTenant,
    ConcurrentDictionary<string, ConcurrentDictionary<string, NOFStateMachineContext>> StateMachineContextsByTenant,
    ConcurrentDictionary<string, IInMemoryPersistencePartition> CustomPartitions);

internal interface IInMemoryPersistencePartition
{
    IInMemoryPersistencePartition Clone();
}

public sealed class InMemoryPersistencePartition<TAggregateRoot, TKey> : IInMemoryPersistencePartition
    where TAggregateRoot : class
    where TKey : notnull
{
    private readonly Func<TAggregateRoot, TKey> _keySelector;
    private readonly Func<TAggregateRoot, TAggregateRoot> _cloner;
    private readonly IEqualityComparer<TKey>? _keyComparer;

    public InMemoryPersistencePartition(
        Func<TAggregateRoot, TKey> keySelector,
        Func<TAggregateRoot, TAggregateRoot> cloner,
        IEqualityComparer<TKey>? keyComparer = null)
    {
        _keySelector = keySelector;
        _cloner = cloner;
        _keyComparer = keyComparer;
        Items = keyComparer is null
            ? new ConcurrentDictionary<TKey, TAggregateRoot>()
            : new ConcurrentDictionary<TKey, TAggregateRoot>(keyComparer);
    }

    public ConcurrentDictionary<TKey, TAggregateRoot> Items { get; private set; }

    IInMemoryPersistencePartition IInMemoryPersistencePartition.Clone()
    {
        var clone = new InMemoryPersistencePartition<TAggregateRoot, TKey>(_keySelector, _cloner, _keyComparer);
        foreach (var pair in Items)
        {
            clone.Items[pair.Key] = _cloner(pair.Value);
        }

        return clone;
    }
}
