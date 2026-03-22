using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace NOF.Hosting.Maui;

/// <summary>
/// Adapts a <see cref="MauiApp"/> to the <see cref="IHost"/> interface
/// so it can participate in the NOF application initialization pipeline.
/// </summary>
/// <remarks>
/// <see cref="StartAsync"/> resolves all registered <see cref="IHostedService"/> instances
/// and starts them in registration order, mirroring the behavior of the generic <c>Host</c>.
/// <see cref="StopAsync"/> stops them in reverse order.
/// </remarks>
public sealed class NOFMauiApp : IHost, IAsyncDisposable
{
    private IHostedService[]? _hostedServices;
    private bool _stopped;

    internal NOFMauiApp(MauiApp mauiApp)
    {
        MauiApp = mauiApp;
    }

    public MauiApp MauiApp { get; }

    /// <inheritdoc />
    public IServiceProvider Services => MauiApp.Services;

    /// <summary>
    /// The application's configured <see cref="IConfiguration"/>.
    /// </summary>
    public IConfiguration Configuration => MauiApp.Configuration;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _hostedServices = [.. Services.GetServices<IHostedService>()];
        foreach (var service in _hostedServices)
        {
            await service.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        MauiApp.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        await MauiApp.DisposeAsync().ConfigureAwait(false);
    }
}
