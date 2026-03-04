namespace NOF.Contract;

/// <summary>
/// Triggers source generation of a service interface and its implementations
/// for request types marked with <see cref="PublicApiAttribute"/>.
/// Place on a <c>partial interface</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, Inherited = false)]
public sealed class GenerateServiceAttribute : Attribute
{
    /// <summary>
    /// Namespaces to scan for <see cref="PublicApiAttribute"/>-annotated request types.
    /// If empty, only the namespace of the annotated interface is scanned.
    /// </summary>
    public string[]? Namespaces { get; init; }

    /// <summary>
    /// Whether to generate an HTTP client implementation (uses <see cref="System.Net.Http.HttpClient"/>).
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool GenerateHttpClient { get; init; } = true;

    /// <summary>
    /// Whether to generate an <see cref="IRequestSender"/>-based implementation for in-process calls.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool GenerateRequestSenderClient { get; init; } = true;

    /// <summary>
    /// Additional request types to include in the generated interface.
    /// Each type must implement <see cref="IRequest"/> or <see cref="IRequest{TResponse}"/>
    /// and must be annotated with <see cref="PublicApiAttribute"/>.
    /// </summary>
    public Type[]? ExtraTypes { get; init; }
}
