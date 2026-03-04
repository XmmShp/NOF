namespace NOF.Contract;

/// <summary>
/// Marks a request type as a public API operation.
/// This attribute is required for the request to be included in generated service interfaces.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PublicApiAttribute : Attribute
{
    /// <summary>
    /// Operation name for generating client method names.
    /// If null, uses the request type name (without "Request" suffix).
    /// </summary>
    public string? OperationName { get; init; }
}
