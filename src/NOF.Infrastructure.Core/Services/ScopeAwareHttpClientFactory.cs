using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using NOF.Infrastructure.Abstraction;

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
internal class ScopeAwareHttpClientFactory : IHttpClientFactory
{
    private readonly IServiceProvider _scopeServiceProvider;
    private readonly IHttpMessageHandlerFactory _handlerFactory;
    private readonly IOptionsMonitor<HttpClientFactoryOptions> _hcfOptions;
    private readonly IOptionsMonitor<ScopeAwareHttpClientFactoryOptions> _scopeAwareOptions;

    /// <summary>
    /// A decorator over the real <see cref="IHttpClientFactory"/> that resolves
    /// designated <see cref="DelegatingHandler"/>s from the <b>current DI scope</b>
    /// (e.g. Blazor circuit scope) and wraps them around the cached handler pipeline.
    /// <para>
    /// Handlers that are NOT marked as scope-aware are unaffected — the factory
    /// simply delegates to the inner factory for those clients.
    /// </para>
    /// </summary>
    public ScopeAwareHttpClientFactory(IServiceProvider scopeServiceProvider,
        IHttpMessageHandlerFactory handlerFactory,
        IOptionsMonitor<HttpClientFactoryOptions> hcfOptions,
        IOptionsMonitor<ScopeAwareHttpClientFactoryOptions> scopeAwareOptions)
    {
        _scopeServiceProvider = scopeServiceProvider;
        _handlerFactory = handlerFactory;
        _hcfOptions = hcfOptions;
        _scopeAwareOptions = scopeAwareOptions;
    }

    public HttpClient CreateClient(string name)
    {
        var options = _scopeAwareOptions.Get(name);

        // Fast path: no scope-aware handlers configured for this client name.
        if (options.HandlerTypes.Count == 0)
        {
            return BuildClientFromHandler(_handlerFactory.CreateHandler(name), name);
        }

        // Build the handler chain: innermost is the cached pipeline from
        // IHttpMessageHandlerFactory, outermost are the scope-aware handlers
        // resolved from the current scope (in registration order).
        var pipeline = _handlerFactory.CreateHandler(name);

        foreach (var handlerType in options.HandlerTypes)
        {
            var delegating = (DelegatingHandler)_scopeServiceProvider.GetRequiredService(handlerType);
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

        var clientOptions = _hcfOptions.Get(name);
        foreach (var action in clientOptions.HttpClientActions)
        {
            action(client);
        }

        return client;
    }
}
