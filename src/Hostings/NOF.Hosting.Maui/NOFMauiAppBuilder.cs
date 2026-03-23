using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Infrastructure;

namespace NOF.Hosting.Maui;

public class NOFMauiAppBuilder : NOFAppBuilder<NOFMauiApp>
{
    public MauiAppBuilder MauiAppBuilder { get; }

    protected NOFMauiAppBuilder(MauiAppBuilder mauiAppBuilder)
    {
        MauiAppBuilder = mauiAppBuilder;
    }

    public static NOFMauiAppBuilder Create(bool useDefaults = true)
    {
        var builder = new NOFMauiAppBuilder(MauiApp.CreateBuilder(useDefaults));
        builder.AddInfrastructureDefaults();
        return builder;
    }

    /// <inheritdoc />
    protected override Task<NOFMauiApp> BuildApplicationAsync()
    {
        return Task.FromResult(new NOFMauiApp(MauiAppBuilder.Build()));
    }

    /// <inheritdoc />
    public override void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
    {
        MauiAppBuilder.ConfigureContainer(factory, configure);
    }

    /// <inheritdoc />
    public override IDictionary<object, object> Properties => MauiAppBuilder.Properties;

    /// <inheritdoc />
    public override IConfigurationManager Configuration => MauiAppBuilder.Configuration;

    /// <inheritdoc />
    public override IHostEnvironment Environment => MauiAppBuilder.Environment;

    /// <inheritdoc />
    public override ILoggingBuilder Logging => MauiAppBuilder.Logging;

    /// <inheritdoc />
    public override IMetricsBuilder Metrics => ((IHostApplicationBuilder)MauiAppBuilder).Metrics;

    /// <inheritdoc />
    public override IServiceCollection Services => MauiAppBuilder.Services;
}
