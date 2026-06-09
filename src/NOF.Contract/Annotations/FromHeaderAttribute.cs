namespace NOF.Contract;

/// <summary>
/// Marks a request property as bound from a transport header.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class FromHeaderAttribute : Attribute
{
    /// <summary>
    /// Creates a header binding attribute.
    /// </summary>
    /// <param name="headerName">The transport header name.</param>
    public FromHeaderAttribute(string headerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        HeaderName = headerName;
    }

    /// <summary>
    /// Gets the transport header name.
    /// </summary>
    public string HeaderName { get; }

    /// <summary>
    /// Gets or sets an optional scheme/prefix to trim from the header value, such as Bearer.
    /// </summary>
    public string? Prefix { get; set; }
}
