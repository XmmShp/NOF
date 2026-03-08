using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace NOF.Hosting.BlazorWebAssembly;

public sealed class NOFWebAssemblyHost : IHost, IAsyncDisposable
{
    private readonly WebAssemblyHost _innerWebAssemblyHost;

    internal NOFWebAssemblyHost(WebAssemblyHost webAssemblyHost)
    {
        _innerWebAssemblyHost = webAssemblyHost;
    }

    public IServiceProvider Services => _innerWebAssemblyHost.Services;

    public IConfiguration Configuration => _innerWebAssemblyHost.Configuration;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync() => _innerWebAssemblyHost.DisposeAsync();

    public Task RunAsync() => _innerWebAssemblyHost.RunAsync();
}
