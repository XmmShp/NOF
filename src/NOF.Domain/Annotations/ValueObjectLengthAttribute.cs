namespace NOF.Domain;

/// <summary>
/// Declares the string-length range accepted by a string-backed value object.
/// </summary>
/// <remarks>
/// This attribute is only valid on structs implementing <c>IValueObject&lt;string&gt;</c>.
/// The value-object source generator enforces the constraint after normalization, while
/// persistence providers can use the same metadata to configure the corresponding column.
/// </remarks>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class ValueObjectLengthAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectLengthAttribute"/> class.
    /// </summary>
    /// <param name="maximumLength">The maximum <see cref="string.Length"/> accepted by the value object.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumLength"/> is not positive.</exception>
    public ValueObjectLengthAttribute(int maximumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);
        MaximumLength = maximumLength;
    }

    /// <summary>
    /// Gets the maximum <see cref="string.Length"/> accepted by the value object.
    /// </summary>
    public int MaximumLength { get; }

    /// <summary>
    /// Gets or sets the minimum <see cref="string.Length"/> accepted by the value object.
    /// The default value is <c>0</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is negative or greater than <see cref="MaximumLength"/>.
    /// </exception>
    public int MinimumLength
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(MinimumLength));
            if (value > MaximumLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MinimumLength),
                    value,
                    $"Minimum length cannot exceed maximum length {MaximumLength}.");
            }

            field = value;
        }
    }
}
