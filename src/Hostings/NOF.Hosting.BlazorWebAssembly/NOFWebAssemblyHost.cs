using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NOF.Hosting.BlazorWebAssembly;

public sealed class NOFWebAssemblyHost : IHost, IAsyncDisposable
{
    private IHostedService[]? _hostedServices;
    private bool _stopped;

    public WebAssemblyHost WebAssemblyHost { get; }

    internal NOFWebAssemblyHost(WebAssemblyHost webAssemblyHost)
    {
        WebAssemblyHost = webAssemblyHost;
    }

    public IServiceProvider Services => WebAssemblyHost.Services;

    public IConfiguration Configuration => WebAssemblyHost.Configuration;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _hostedServices = [.. Services.GetServices<IHostedService>()];
        foreach (var service in _hostedServices)
        {
            await service.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_stopped || _hostedServices is null)
        {
            return;
        }

        _stopped = true;
        foreach (var service in _hostedServices.Reverse())
        {
            await service.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        await WebAssemblyHost.DisposeAsync().ConfigureAwait(false);
    }

    public Task RunAsync() => WebAssemblyHost.RunAsync();
}
