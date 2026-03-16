using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;

namespace NOF.Infrastructure.Core;

public sealed class InitializingServiceProvider(IServiceProvider innerProvider) : IServiceScopeFactory, IKeyedServiceProvider
{
    private IServiceScopeFactory? _scopeFactory;

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceProvider))
        {
            return this;
        }

        if (serviceType == typeof(IServiceScopeFactory))
        {
            return _scopeFactory ??= new InitializingServiceScopeFactory(innerProvider.GetRequiredService<IServiceScopeFactory>());
        }

        return InitializeIfNeeded(innerProvider.GetService(serviceType));
    }

    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        return innerProvider is IKeyedServiceProvider keyedServiceProvider
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
        return (_scopeFactory ??= new InitializingServiceScopeFactory(innerProvider.GetRequiredService<IServiceScopeFactory>())).CreateScope();
    }

    private static object? InitializeIfNeeded(object? instance)
    {
        if (instance is IInitializable initializable && !initializable.IsInitialized)
        {
            initializable.Initialize();
        }

        return instance;
    }
}
