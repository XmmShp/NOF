namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Per-client-name options that track which <see cref="DelegatingHandler"/> types
/// should be resolved from the current DI scope.
/// </summary>
internal class ScopeAwareHttpClientFactoryOptions
{
    /// <summary>
    /// The ordered list of <see cref="DelegatingHandler"/> types to resolve from the
    /// current scope and wrap around the cached handler pipeline.
    /// </summary>
    public List<Type> HandlerTypes { get; } = [];
}
