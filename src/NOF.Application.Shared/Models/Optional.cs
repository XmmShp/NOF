using System.Diagnostics.CodeAnalysis;

namespace NOF;

public readonly struct Optional<T>
{
    public T? Value { get; init; }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool HasValue { get; init; }

    public static implicit operator Optional<T>(T value)
    {
        return new Optional<T> { Value = value, HasValue = true };
    }

    public static Optional<T> ConvertFrom<TFrom>(Optional<TFrom> value, Func<TFrom, T> valueFactory)
    {
        return value.HasValue
            ? valueFactory(value.Value)
            : Unspecified;
    }

    public static Optional<T> Unspecified { get; } = new();
}