namespace NOF.Contract;

/// <summary>
/// Triggers source generation of a service HTTP client implementation
/// from user-declared service interface method signatures (RPC-style IDL).
/// Place on a <c>partial interface</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class GenerateServiceAttribute : Attribute
{
    /// <summary>
    /// Whether to generate an HTTP client implementation (uses <see cref="HttpClient"/>).
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool GenerateHttpClient { get; init; } = true;
}
