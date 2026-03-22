using System.Collections.Concurrent;

namespace NOF.Infrastructure.Memory;

public sealed class MemoryPersistenceContext : ICloneable
{
    public MemoryPersistenceContext() : this(new ConcurrentDictionary<Type, IMemoryPersistenceTable>())
    {
    }

    public MemoryPersistenceContext(ConcurrentDictionary<Type, IMemoryPersistenceTable> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);

        Tables = tables;
    }

    public ConcurrentDictionary<Type, IMemoryPersistenceTable> Tables { get; }

    public List<TAggregateRoot> Set<TAggregateRoot>()
        where TAggregateRoot : class, ICloneable
        => ((MemoryPersistenceTable<TAggregateRoot>)Tables.GetOrAdd(typeof(TAggregateRoot), _ => new MemoryPersistenceTable<TAggregateRoot>())).Items;

    public object Clone()
    {
        var tables = new ConcurrentDictionary<Type, IMemoryPersistenceTable>();
        foreach (var table in Tables)
        {
            tables[table.Key] = (IMemoryPersistenceTable)table.Value.Clone();
        }

        return new MemoryPersistenceContext(tables);
    }
}
