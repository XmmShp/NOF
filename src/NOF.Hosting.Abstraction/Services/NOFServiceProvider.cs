using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;

namespace NOF.Hosting;

public sealed class NOFServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
{
    private readonly DefaultServiceProviderFactory _innerFactory = new();

    public IServiceCollection CreateBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }

    public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
    {
        ArgumentNullException.ThrowIfNull(containerBuilder);
        var innerProvider = _innerFactory.CreateServiceProvider(containerBuilder);
        return new NOFServiceProvider(innerProvider);
    }
}

public sealed class NOFServiceProvider : IServiceProvider, IServiceScopeFactory, IAsyncDisposable, IDisposable
{
    private readonly IServiceProvider _innerProvider;
    private readonly IServiceScopeFactory _innerScopeFactory;
    private readonly IDisposable? _innerDisposable;
    private readonly IAsyncDisposable? _innerAsyncDisposable;

    public NOFServiceProvider(IServiceProvider innerProvider)
    {
        _innerProvider = innerProvider;
        _innerScopeFactory = innerProvider.GetRequiredService<IServiceScopeFactory>();
        _innerDisposable = innerProvider as IDisposable;
        _innerAsyncDisposable = innerProvider as IAsyncDisposable;
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceProvider) || serviceType == typeof(IServiceScopeFactory))
        {
            return this;
        }

        return _innerProvider.GetService(serviceType);
    }

    public IServiceScope CreateScope()
        => new NOFServiceScope(_innerScopeFactory.CreateScope());

    public ValueTask DisposeAsync()
    {
        if (_innerAsyncDisposable is not null)
        {
            return _innerAsyncDisposable.DisposeAsync();
        }

        _innerDisposable?.Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _innerDisposable?.Dispose();
    }
}

public sealed class NOFServiceScope : IServiceScope, IAsyncDisposable
{
    private readonly IServiceScope _innerScope;
    private readonly IAsyncDisposable? _innerAsyncDisposable;

    public NOFServiceScope(IServiceScope innerScope)
    {
        _innerScope = innerScope;
        ServiceProvider = new ScopedNOFServiceProvider(innerScope.ServiceProvider);
        _innerAsyncDisposable = innerScope as IAsyncDisposable;
    }

    public IServiceProvider ServiceProvider { get; }

    public void Dispose()
    {
        _innerScope.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        if (_innerAsyncDisposable is { } asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }

        _innerScope.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class ScopedNOFServiceProvider : IServiceProvider, IServiceScopeFactory
    {
        private readonly IServiceProvider _innerProvider;
        private readonly IServiceScopeFactory _innerScopeFactory;

        public ScopedNOFServiceProvider(IServiceProvider innerProvider)
        {
            _innerProvider = innerProvider;
            _innerScopeFactory = innerProvider.GetRequiredService<IServiceScopeFactory>();
            var daemons = innerProvider.GetServices<IDaemonService>();
            _ = daemons.ToArray();
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceProvider) || serviceType == typeof(IServiceScopeFactory))
            {
                return this;
            }

            return _innerProvider.GetService(serviceType);
        }

        public IServiceScope CreateScope()
            => new NOFServiceScope(_innerScopeFactory.CreateScope());
    }
}
