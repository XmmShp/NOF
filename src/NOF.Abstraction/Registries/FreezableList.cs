using System.Collections;

namespace NOF.Abstraction;

/// <summary>
/// A list that can be frozen to prevent any further modifications.
/// </summary>
public class FreezableList<T> : IList<T>, IReadOnlyList<T>
{
    private readonly List<T> _items = [];

    public bool IsFrozen => IsReadOnly;

    public T this[int index]
    {
        get => _items[index];
        set
        {
            ThrowIfFrozen();
            _items[index] = value;
        }
    }

    public int Count => _items.Count;

    public bool IsReadOnly { get; private set; }

    public void Freeze() => IsReadOnly = true;

    public void Add(T item)
    {
        ThrowIfFrozen();
        _items.Add(item);
    }

    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        ThrowIfFrozen();
        foreach (var item in items)
        {
            _items.Add(item);
        }
    }

    public void AddRange(ReadOnlySpan<T> items)
    {
        ThrowIfFrozen();
        foreach (var item in items)
        {
            _items.Add(item);
        }
    }

    public void Clear()
    {
        ThrowIfFrozen();
        _items.Clear();
    }

    public bool Contains(T item) => _items.Contains(item);

    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    public int IndexOf(T item) => _items.IndexOf(item);

    public void Insert(int index, T item)
    {
        ThrowIfFrozen();
        _items.Insert(index, item);
    }

    public bool Remove(T item)
    {
        ThrowIfFrozen();
        return _items.Remove(item);
    }

    public void RemoveAt(int index)
    {
        ThrowIfFrozen();
        _items.RemoveAt(index);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void ThrowIfFrozen()
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException($"{GetType().Name} is frozen and can no longer be modified.");
        }
    }
}
