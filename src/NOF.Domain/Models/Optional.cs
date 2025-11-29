using System.Diagnostics.CodeAnalysis;

namespace NOF;

public readonly struct Optional
{
    public static Optional<T> Of<T>(T value) => new(value, true);
    public static NoneOptional None { get; } = new();
}

public readonly struct NoneOptional;

public readonly struct Optional<T>
{
    internal Optional(T value, bool hasValue)
    {
        Value = value;
        HasValue = hasValue;
    }

    public T Value
    {
        get
        {
            if (!HasValue)
            {
                throw new InvalidOperationException("Optional does not contain a value.");
            }
            return field;
        }
    }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool HasValue { get; }

    public static implicit operator Optional<T>(NoneOptional optional) => new(default!, false);
}