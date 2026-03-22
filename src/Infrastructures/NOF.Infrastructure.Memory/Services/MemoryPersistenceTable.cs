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
