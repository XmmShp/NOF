namespace NOF;

/// <summary>
/// Provides factory methods for creating <see cref="Optional{T}"/> instances.
/// </summary>
public readonly struct Optional
{
    /// <summary>
    /// Creates an <see cref="Optional{T}"/> that contains the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to wrap. Can be <c>null</c> if <typeparamref name="T"/> is a reference type.</param>
    /// <returns>An <see cref="Optional{T}"/> containing the given value.</returns>
    public static Optional<T> Of<T>(T value) => new(value, true);

    /// <summary>
    /// Represents an absent (empty) optional value.
    /// </summary>
    public static NoneOptional None { get; } = new();
}

/// <summary>
/// A marker type representing the absence of a value in an <see cref="Optional{T}"/>.
/// This type has no members and exists solely to enable implicit conversion to <see cref="Optional{T}"/>.
/// </summary>
public readonly struct NoneOptional;

/// <summary>
/// Represents a value that may or may not be present.
/// This struct provides a type-safe alternative to using <c>null</c> for reference types,
/// and enables explicit handling of missing values for value types.
/// </summary>
/// <typeparam name="T">The type of the optional value.</typeparam>
[OptionalJsonConverter]
public readonly struct Optional<T>
{
    internal Optional(T value, bool hasValue)
    {
        Value = value;
        HasValue = hasValue;
    }

    /// <summary>
    /// Gets the contained value.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="HasValue"/> is <c>false</c>, indicating no value is present.
    /// </exception>
    public T Value
        => HasValue
            ? field
            : throw new InvalidOperationException("Optional does not contain a value.");

    /// <summary>
    /// Gets a value indicating whether this instance contains a valid value.
    /// </summary>
    public bool HasValue { get; }

    /// <summary>
    /// Defines an implicit conversion from <see cref="NoneOptional"/> to <see cref="Optional{T}"/>,
    /// enabling syntax like <c>Optional&lt;int&gt; x = Optional.None;</c>.
    /// </summary>
    /// <param name="optional">The <see cref="NoneOptional"/> instance (ignored).</param>
    /// <returns>An empty <see cref="Optional{T}"/> instance.</returns>
    public static implicit operator Optional<T>(NoneOptional optional) => new(default!, false);
}