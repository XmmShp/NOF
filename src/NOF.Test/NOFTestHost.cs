using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Contract;

namespace NOF.Test;

public sealed class NOFTestHost : IAsyncDisposable, IDisposable
{
    public NOFTestHost(IHost host)
    {
        Host = host;
    }

    public IHost Host { get; }

    public IServiceProvider Services => Host.Services;

    public T GetRequiredService<T>() where T : notnull
    {
        return Services.GetRequiredService<T>();
    }

    public NOFTestScope CreateScope()
    {
        var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.ResolveDaemonServices();
        return new NOFTestScope(scope);
    }

    public async Task SendAsync<TCommand>(TCommand command, Context context, CancellationToken cancellationToken = default)
    {
        using var scope = CreateScope();
        await scope.SendAsync(command, context, cancellationToken);
    }

    public async Task PublishAsync<TNotification>(TNotification notification, Context context, CancellationToken cancellationToken = default)
    {
        using var scope = CreateScope();
        await scope.PublishAsync(notification, context, cancellationToken);
    }

    public void Dispose()
    {
        Host.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        if (Host is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }

        Host.Dispose();
        return ValueTask.CompletedTask;
    }
}
