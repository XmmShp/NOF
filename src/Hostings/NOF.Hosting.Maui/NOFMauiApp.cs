using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace NOF.Hosting.Maui;

/// <summary>
/// Adapts a <see cref="MauiApp"/> to the <see cref="IHost"/> interface
/// so it can participate in the NOF application initialization pipeline.
/// </summary>
public sealed class NOFMauiApp : IHost, IAsyncDisposable
{
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
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose() => MauiApp.Dispose();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => MauiApp.DisposeAsync();
}
