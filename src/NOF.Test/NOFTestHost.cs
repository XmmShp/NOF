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
        return new NOFTestScope(Services.CreateAsyncScope());
    }

    public async Task<Result> SendAsync(object request, CancellationToken cancellationToken = default)
    {
        using var scope = CreateScope();
        return await scope.SendAsync(request, cancellationToken);
    }

    public async Task<Result<TResponse>> SendAsync<TResponse>(object request, CancellationToken cancellationToken = default)
    {
        using var scope = CreateScope();
        return await scope.SendAsync<TResponse>(request, cancellationToken);
    }

    public async Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        using var scope = CreateScope();
        await scope.SendAsync(command, cancellationToken);
    }

    public async Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
    {
        using var scope = CreateScope();
        await scope.PublishAsync(notification, cancellationToken);
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
