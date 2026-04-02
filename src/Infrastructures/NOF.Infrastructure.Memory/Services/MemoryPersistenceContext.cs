using NOF.Infrastructure;
using NOF.Domain;
using System.Collections.Concurrent;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryPersistenceContext : ICloneable
{
    private int _pendingChanges;
    private readonly ConcurrentDictionary<IAggregateRoot, byte> _trackedEntities = new(ReferenceEqualityComparer.Instance);

    public MemoryPersistenceContext() : this(NOFInfrastructureConstants.Tenant.HostId, new ConcurrentDictionary<Type, IMemoryPersistenceTable>())
    {
    }

    public MemoryPersistenceContext(string tenantId, ConcurrentDictionary<Type, IMemoryPersistenceTable> tables)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(tables);

        TenantId = NOFInfrastructureConstants.Tenant.NormalizeTenantId(tenantId);
        Tables = tables;
    }

    public string TenantId { get; }

    public ConcurrentDictionary<Type, IMemoryPersistenceTable> Tables { get; }

    public IList<TAggregateRoot> Set<TAggregateRoot>()
        where TAggregateRoot : class, ICloneable
    {
        if (IsHostOnlyType(typeof(TAggregateRoot)))
        {
            var table = (MemoryPersistenceTable<TAggregateRoot>)Tables.GetOrAdd(
                typeof(TAggregateRoot),
                static _ => new MemoryPersistenceTable<TAggregateRoot>());
            return table.Items;
        }

        var tenantTable = (MemoryTenantPersistenceTable<TAggregateRoot>)Tables.GetOrAdd(
            typeof(TAggregateRoot),
            static _ => new MemoryTenantPersistenceTable<TAggregateRoot>());

        return new TenantScopedList<TAggregateRoot>(TenantId, tenantTable);
    }

    internal bool TrackEntity(IAggregateRoot aggregateRoot)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        if (!_trackedEntities.TryAdd(aggregateRoot, 0))
        {
            return false;
        }

        Interlocked.Increment(ref _pendingChanges);
        return true;
    }

    internal List<IAggregateRoot> ConsumeTrackedEntities()
    {
        var trackedEntities = _trackedEntities.Keys.ToList();
        _trackedEntities.Clear();
        return trackedEntities;
    }

    public object Clone()
    {
        var tables = new ConcurrentDictionary<Type, IMemoryPersistenceTable>();
        foreach (var table in Tables)
        {
            tables[table.Key] = (IMemoryPersistenceTable)table.Value.Clone();
        }

        return new MemoryPersistenceContext(TenantId, tables)
        {
            _pendingChanges = _pendingChanges
        };
    }

    private static bool IsHostOnlyType(Type type)
        => type == typeof(NOFInboxMessage)
            || type == typeof(NOFOutboxMessage)
            || type == typeof(NOFTenant);

    private sealed class TenantScopedList<TAggregateRoot> : IList<TAggregateRoot>
        where TAggregateRoot : class, ICloneable
    {
        private readonly string _tenantId;
        private readonly MemoryTenantPersistenceTable<TAggregateRoot> _table;

        public TenantScopedList(string tenantId, MemoryTenantPersistenceTable<TAggregateRoot> table)
        {
            _tenantId = tenantId;
            _table = table;
        }

        public int Count => _table.Entries.Count(entry => string.Equals(entry.TenantId, _tenantId, StringComparison.Ordinal));

        public bool IsReadOnly => false;

        public TAggregateRoot this[int index]
        {
            get => _table.Entries[TranslateIndex(index)].Entity;
            set
            {
                var translated = TranslateIndex(index);
                _table.Entries[translated] = new MemoryTenantPersistenceTable<TAggregateRoot>.Entry(_tenantId, value);
            }
        }

        public void Add(TAggregateRoot item)
            => _table.Entries.Add(new MemoryTenantPersistenceTable<TAggregateRoot>.Entry(_tenantId, item));

        public void Clear()
        {
            for (var i = _table.Entries.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_table.Entries[i].TenantId, _tenantId, StringComparison.Ordinal))
                {
                    _table.Entries.RemoveAt(i);
                }
            }
        }

        public bool Contains(TAggregateRoot item)
            => _table.Entries.Any(entry =>
                string.Equals(entry.TenantId, _tenantId, StringComparison.Ordinal)
                && (ReferenceEquals(entry.Entity, item) || EqualityComparer<TAggregateRoot>.Default.Equals(entry.Entity, item)));

        public void CopyTo(TAggregateRoot[] array, int arrayIndex)
        {
            foreach (var entity in this)
            {
                array[arrayIndex++] = entity;
            }
        }

        public IEnumerator<TAggregateRoot> GetEnumerator()
        {
            foreach (var entry in _table.Entries)
            {
                if (string.Equals(entry.TenantId, _tenantId, StringComparison.Ordinal))
                {
                    yield return entry.Entity;
                }
            }
        }

        public int IndexOf(TAggregateRoot item)
        {
            var index = 0;
            foreach (var entry in _table.Entries)
            {
                if (!string.Equals(entry.TenantId, _tenantId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (ReferenceEquals(entry.Entity, item) || EqualityComparer<TAggregateRoot>.Default.Equals(entry.Entity, item))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        public void Insert(int index, TAggregateRoot item)
        {
            if (index == Count)
            {
                Add(item);
                return;
            }

            var translated = TranslateIndex(index);
            _table.Entries.Insert(translated, new MemoryTenantPersistenceTable<TAggregateRoot>.Entry(_tenantId, item));
        }

        public bool Remove(TAggregateRoot item)
        {
            for (var i = _table.Entries.Count - 1; i >= 0; i--)
            {
                var entry = _table.Entries[i];
                if (!string.Equals(entry.TenantId, _tenantId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (ReferenceEquals(entry.Entity, item) || EqualityComparer<TAggregateRoot>.Default.Equals(entry.Entity, item))
                {
                    _table.Entries.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public void RemoveAt(int index)
            => _table.Entries.RemoveAt(TranslateIndex(index));

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();

        private int TranslateIndex(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var current = 0;
            for (var i = 0; i < _table.Entries.Count; i++)
            {
                if (!string.Equals(_table.Entries[i].TenantId, _tenantId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (current == index)
                {
                    return i;
                }

                current++;
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
