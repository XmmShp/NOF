using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace NOF.Infrastructure.Core;

/// <summary>
/// A decorator over the real <see cref="IHttpClientFactory"/> that resolves
/// designated <see cref="DelegatingHandler"/>s from the <b>current DI scope</b>
/// (e.g. Blazor circuit scope) and wraps them around the cached handler pipeline.
/// <para>
/// Handlers that are NOT marked as scope-aware are unaffected — the factory
/// simply delegates to the inner factory for those clients.
/// </para>
/// </summary>
public class ScopeAwareHttpClientFactory(
    IServiceProvider scopeServiceProvider,
    IHttpMessageHandlerFactory handlerFactory,
    IOptionsMonitor<HttpClientFactoryOptions> hcfOptions,
    IOptionsMonitor<ScopeAwareHttpClientFactoryOptions> scopeAwareOptions)
    : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        var options = scopeAwareOptions.Get(name);

        // Fast path: no scope-aware handlers configured for this client name.
        if (options.HandlerTypes.Count == 0)
        {
            return BuildClientFromHandler(handlerFactory.CreateHandler(name), name);
        }

        // Build the handler chain: innermost is the cached pipeline from
        // IHttpMessageHandlerFactory, outermost are the scope-aware handlers
        // resolved from the current scope (in registration order).
        var pipeline = handlerFactory.CreateHandler(name);

        foreach (var handlerType in options.HandlerTypes)
        {
            var delegating = (DelegatingHandler)scopeServiceProvider.GetRequiredService(handlerType);
            if (delegating.InnerHandler is not null)
            {
                throw new InvalidOperationException(
                    $"Scope-aware handler {handlerType.Name} already has an InnerHandler set. " +
                    $"Make sure it is registered as Transient.");
            }

            delegating.InnerHandler = pipeline;
            pipeline = delegating;
        }

        return BuildClientFromHandler(pipeline, name);
    }

    private HttpClient BuildClientFromHandler(HttpMessageHandler handler, string name)
    {
        var client = new HttpClient(handler, disposeHandler: false);

        var clientOptions = hcfOptions.Get(name);
        foreach (var action in clientOptions.HttpClientActions)
        {
            action(client);
        }

        return client;
    }
}

/// <summary>
/// Per-client-name options that track which <see cref="DelegatingHandler"/> types
/// should be resolved from the current DI scope.
/// </summary>
public class ScopeAwareHttpClientFactoryOptions
{
    /// <summary>
    /// The ordered list of <see cref="DelegatingHandler"/> types to resolve from the
    /// current scope and wrap around the cached handler pipeline.
    /// </summary>
    public List<Type> HandlerTypes { get; } = [];
}
