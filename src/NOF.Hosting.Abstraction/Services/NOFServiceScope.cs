using Microsoft.Extensions.DependencyInjection;

namespace NOF.Hosting;

public sealed class NOFServiceScope(IServiceScope innerScope) : IServiceScope, IAsyncDisposable
{
    public IServiceProvider ServiceProvider
        => field ??= new NOFServiceProvider(innerScope.ServiceProvider);

    public void Dispose()
        => innerScope.Dispose();

    public ValueTask DisposeAsync()
    {
        if (innerScope is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }

        innerScope.Dispose();
        return ValueTask.CompletedTask;
    }
}
