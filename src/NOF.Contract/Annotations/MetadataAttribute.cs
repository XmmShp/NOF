namespace NOF.Annotation;

/// <summary>
/// Attaches a simple key-value metadata pair to a program element.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
public class MetadataAttribute : Attribute
{
    /// <summary>
    /// The metadata key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The metadata value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataAttribute"/> class.
    /// </summary>
    /// <param name="key">Metadata key.</param>
    /// <param name="value">Metadata value.</param>
    public MetadataAttribute(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        Key = key;
        Value = value;
    }
}
