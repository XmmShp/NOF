using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace NOF.Hosting.BlazorWebAssembly;

public sealed class NOFWebAssemblyHost : IHost, IAsyncDisposable
{
    public WebAssemblyHost WebAssemblyHost { get; }

    internal NOFWebAssemblyHost(WebAssemblyHost webAssemblyHost)
    {
        WebAssemblyHost = webAssemblyHost;
    }

    public IServiceProvider Services => WebAssemblyHost.Services;

    public IConfiguration Configuration => WebAssemblyHost.Configuration;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync() => WebAssemblyHost.DisposeAsync();

    public Task RunAsync() => WebAssemblyHost.RunAsync();
}
