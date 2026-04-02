namespace NOF.Infrastructure.Memory;

public interface IMemoryPersistenceTable : ICloneable
{
    List<object> Items { get; }
}

public sealed class MemoryPersistenceTable<TAggregateRoot> : IMemoryPersistenceTable
    where TAggregateRoot : class, ICloneable
{
    List<object> IMemoryPersistenceTable.Items => Items.Cast<object>().ToList();
    public List<TAggregateRoot> Items { get; } = [];

    public object Clone()
    {
        var clone = new MemoryPersistenceTable<TAggregateRoot>();
        foreach (var item in Items)
        {
            clone.Items.Add((TAggregateRoot)item.Clone());
        }

        return clone;
    }
}

public sealed class MemoryTenantPersistenceTable<TAggregateRoot> : IMemoryPersistenceTable
    where TAggregateRoot : class, ICloneable
{
    internal readonly record struct Entry(string TenantId, TAggregateRoot Entity);

    List<object> IMemoryPersistenceTable.Items => Entries.Select(entry => (object)entry.Entity).ToList();

    internal List<Entry> Entries { get; } = [];

    public object Clone()
    {
        var clone = new MemoryTenantPersistenceTable<TAggregateRoot>();
        foreach (var entry in Entries)
        {
            clone.Entries.Add(new Entry(entry.TenantId, (TAggregateRoot)entry.Entity.Clone()));
        }

        return clone;
    }
}
