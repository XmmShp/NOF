using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Infrastructure.Core;

namespace NOF.Hosting.Maui;

/// <summary>
/// Adapts a <see cref="MauiApp"/> to the <see cref="IHost"/> interface
/// so it can participate in the NOF application initialization pipeline.
/// </summary>
public sealed class MauiHostAdapter(MauiApp mauiApp) : IHost
{
    public MauiApp MauiApp { get; } = mauiApp;

    /// <inheritdoc />
    public IServiceProvider Services => MauiApp.Services;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose() => MauiApp.Dispose();
}

public class NOFMauiAppBuilder : NOFAppBuilder<MauiHostAdapter>
{
    public MauiAppBuilder InnerBuilder { get; }

    protected NOFMauiAppBuilder(MauiAppBuilder innerBuilder)
    {
        InnerBuilder = innerBuilder;
    }

    public static NOFMauiAppBuilder Create(MauiAppBuilder innerBuilder)
    {
        return new NOFMauiAppBuilder(innerBuilder);
    }

    /// <inheritdoc />
    protected override Task<MauiHostAdapter> BuildApplicationAsync()
    {
        return Task.FromResult(new MauiHostAdapter(InnerBuilder.Build()));
    }

    /// <inheritdoc />
    public override void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
    {
        InnerBuilder.ConfigureContainer(factory, configure);
    }

    /// <inheritdoc />
    public override IDictionary<object, object> Properties => InnerBuilder.Properties;

    /// <inheritdoc />
    public override IConfigurationManager Configuration => InnerBuilder.Configuration;

    /// <inheritdoc />
    public override IHostEnvironment Environment => InnerBuilder.Environment;

    /// <inheritdoc />
    public override ILoggingBuilder Logging => InnerBuilder.Logging;

    /// <inheritdoc />
    public override IMetricsBuilder Metrics => ((IHostApplicationBuilder)InnerBuilder).Metrics;

    /// <inheritdoc />
    public override IServiceCollection Services => InnerBuilder.Services;
}
