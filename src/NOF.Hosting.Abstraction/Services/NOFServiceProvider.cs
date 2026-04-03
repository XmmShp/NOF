using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;

namespace NOF.Hosting;

public sealed class NOFServiceProvider : IServiceScopeFactory, IKeyedServiceProvider
{
    private readonly IServiceProvider _innerProvider;
    private IServiceScopeFactory? _scopeFactory;

    public NOFServiceProvider(IServiceProvider innerProvider)
    {
        ArgumentNullException.ThrowIfNull(innerProvider);
        _innerProvider = innerProvider;

        // Force daemon service resolution every time a NOF service provider is created.
        _ = _innerProvider.GetServices<IDaemonService>().ToArray();
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceProvider))
        {
            return this;
        }

        if (serviceType == typeof(IServiceScopeFactory))
        {
            return _scopeFactory ??= new NOFServiceScopeFactory(_innerProvider.GetRequiredService<IServiceScopeFactory>());
        }

        return InitializeIfNeeded(_innerProvider.GetService(serviceType));
    }

    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        return _innerProvider is IKeyedServiceProvider keyedServiceProvider
            ? InitializeIfNeeded(keyedServiceProvider.GetKeyedService(serviceType, serviceKey))
            : null;
    }

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
    {
        return GetKeyedService(serviceType, serviceKey)
               ?? throw new InvalidOperationException($"No service for type '{serviceType}' has been registered.");
    }

    public IServiceScope CreateScope()
    {
        return (_scopeFactory ??= new NOFServiceScopeFactory(_innerProvider.GetRequiredService<IServiceScopeFactory>())).CreateScope();
    }

    private static object? InitializeIfNeeded(object? instance)
    {
        if (instance is IInitializable { IsInitialized: false } initializable)
        {
            initializable.Initialize();
        }

        return instance;
    }
}
