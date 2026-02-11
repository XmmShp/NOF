namespace NOF.Contract;

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

    /// <summary>
    /// Returns the contained value if present; otherwise, returns the specified default value.
    /// </summary>
    /// <param name="defaultValue">The value to return if no value is present.</param>
    /// <returns>The contained value or <paramref name="defaultValue"/>.</returns>
    public T ValueOr(T defaultValue)
        => ValueOr(() => defaultValue);

    /// <summary>
    /// Returns the contained value if present; otherwise, invokes the factory function to produce a default value.
    /// This overload avoids unnecessary evaluation of the default when a value is present.
    /// </summary>
    /// <param name="defaultValueFactory">A function that produces a default value when needed.</param>
    /// <returns>The contained value or the result of <paramref name="defaultValueFactory"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="defaultValueFactory"/> is <c>null</c>.
    /// </exception>
    public T ValueOr(Func<T> defaultValueFactory)
    {
        if (HasValue)
        {
            return Value;
        }

        ArgumentNullException.ThrowIfNull(defaultValueFactory);
        return defaultValueFactory();
    }

    /// <summary>
    /// Executes one of two actions based on whether a value is present.
    /// </summary>
    /// <param name="some">Action to execute if a value is present. Receives the value as input.</param>
    /// <param name="none">Action to execute if no value is present.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if either <paramref name="some"/> or <paramref name="none"/> is <c>null</c>.
    /// </exception>
    public void Match(Action<T> some, Action none)
    {
        if (HasValue)
        {
            ArgumentNullException.ThrowIfNull(some);
            some(Value);
        }
        else
        {
            ArgumentNullException.ThrowIfNull(none);
            none();
        }
    }

    /// <summary>
    /// Executes an action if a value is present.
    /// </summary>
    /// <param name="action">Action to execute with the value if present.</param>
    /// <returns>The original optional for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="action"/> is <c>null</c>.
    /// </exception>
    public void IfSome(Action<T> action)
        => Match(action, () => { });

    /// <summary>
    /// Executes an action if no value is present.
    /// </summary>
    /// <param name="action">Action to execute if no value is present.</param>
    /// <returns>The original optional for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="action"/> is <c>null</c>.
    /// </exception>
    public void IfNone(Action action)
        => Match(_ => { }, action);

    /// <summary>
    /// Transforms the optional value into a result of type <typeparamref name="TResult"/>.
    /// If a value is present, applies the <paramref name="some"/> function; otherwise, uses the <paramref name="none"/> function.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="some">Function to apply if a value is present.</param>
    /// <param name="none">Function to apply if no value is present.</param>
    /// <returns>The result of applying the appropriate function.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if either <paramref name="some"/> or <paramref name="none"/> is <c>null</c>.
    /// </exception>
    public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none)
    {
        if (HasValue)
        {
            ArgumentNullException.ThrowIfNull(some);
            return some(Value);
        }

        ArgumentNullException.ThrowIfNull(none);
        return none();
    }

    /// <summary>
    /// Transforms the contained value using the specified function, returning a new <see cref="Optional{TResult}"/>.
    /// If no value is present, returns <see cref="Optional.None"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <param name="valueFactory">The function to apply to the contained value.</param>
    /// <returns>
    /// An <see cref="Optional{TResult}"/> containing the transformed value, or <see cref="Optional.None"/> if no value was present.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="valueFactory"/> is <c>null</c>.
    /// </exception>
    public Optional<TResult> Map<TResult>(Func<T, TResult> valueFactory)
    {
        if (!HasValue)
        {
            return Optional.None;
        }

        ArgumentNullException.ThrowIfNull(valueFactory);
        return Optional.Of(valueFactory(Value));
    }

    /// <summary>
    /// Transforms the contained value into a nullable reference or value type.
    /// If a value is present, returns the result of the transformation; otherwise, returns <c>default</c> (typically <c>null</c> for reference types).
    /// </summary>
    /// <typeparam name="TResult">The type of the result, typically a reference type or nullable value type.</typeparam>
    /// <param name="valueFactory">The function to apply to the contained value.</param>
    /// <returns>
    /// The transformed value if present; otherwise, <c>default(TResult)</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="valueFactory"/> is <c>null</c>.
    /// </exception>
    public TResult? MapAsNullable<TResult>(Func<T, TResult> valueFactory)
    {
        if (!HasValue)
        {
            return default;
        }

        ArgumentNullException.ThrowIfNull(valueFactory);
        return valueFactory(Value);
    }
}
