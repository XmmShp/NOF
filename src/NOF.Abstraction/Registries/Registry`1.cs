namespace NOF.Abstraction;

public abstract class Registry<T>
{
    private readonly Lock _freezeGate = new();
    private readonly FreezableList<T> _value = [];

    protected event Action? Frozen;

    protected virtual FreezableList<T> Items => _value;

    public virtual void Add(T item)
    {
        Items.Add(item);
    }

    public bool Remove(T item)
    {
        return RemoveWhere(current => EqualityComparer<T>.Default.Equals(current, item)) > 0;
    }

    public virtual int RemoveWhere(Predicate<T> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var removed = 0;
        for (var index = Items.Count - 1; index >= 0; index--)
        {
            if (!predicate(Items[index]))
            {
                continue;
            }

            Items.RemoveAt(index);
            removed++;
        }

        return removed;
    }

    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            Add(item);
        }
    }

    public void AddRange(ReadOnlySpan<T> items)
    {
        foreach (var item in items)
        {
            Add(item);
        }
    }

    public virtual IReadOnlyList<T> Freeze()
    {
        if (Items.IsFrozen)
        {
            return Items;
        }

        lock (_freezeGate)
        {
            if (!Items.IsFrozen)
            {
                Items.Freeze();
                Frozen?.Invoke();
            }
        }

        return Items;
    }
}
