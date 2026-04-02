using System.Collections.Concurrent;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryPersistenceStore : ICloneable
{
    private ConcurrentDictionary<Type, IMemoryPersistenceTable> _tables = new();

    internal void RestoreFrom(MemoryPersistenceStore snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _tables.Clear();
        foreach (var table in snapshot._tables)
        {
            _tables[table.Key] = (IMemoryPersistenceTable)table.Value.Clone();
        }
    }

    public MemoryPersistenceContext CreateContext(string tenantId)
        => new(tenantId, _tables);

    public object Clone()
        => new MemoryPersistenceStore
        {
            _tables = CloneTableDictionary(_tables)
        };

    private static ConcurrentDictionary<Type, IMemoryPersistenceTable> CloneTableDictionary(
        ConcurrentDictionary<Type, IMemoryPersistenceTable> source)
    {
        var clone = new ConcurrentDictionary<Type, IMemoryPersistenceTable>();

        foreach (var table in source)
        {
            clone[table.Key] = (IMemoryPersistenceTable)table.Value.Clone();
        }

        return clone;
    }
}
