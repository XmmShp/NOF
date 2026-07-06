using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Hosting;

namespace NOF.Test;

public sealed class NOFTestAppBuilder : IHostApplicationBuilder
{
    public HostApplicationBuilder InnerBuilder { get; }

    private NOFTestAppBuilder(string[]? args)
    {
        InnerBuilder = Host.CreateApplicationBuilder(args ?? []);
        ConfigureDefaultTestServices();
    }

    public static NOFTestAppBuilder Create(string[]? args = null)
    {
        return new NOFTestAppBuilder(args);
    }

    public async Task<NOFTestHost> BuildTestHostAsync()
    {
        var host = await BuildAsync();
        return new NOFTestHost(host);
    }

    public Task<IHost> BuildAsync()
        => this.BuildNOFAsync(InnerBuilder.Build);

    private void ConfigureDefaultTestServices()
    {
        this.AddNOFInfrastructure();
    }

    public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull
    {
        InnerBuilder.ConfigureContainer(factory, configure);
    }

    public IDictionary<object, object> Properties => ((IHostApplicationBuilder)InnerBuilder).Properties;

    public IConfigurationManager Configuration => InnerBuilder.Configuration;

    public IHostEnvironment Environment => InnerBuilder.Environment;

    public ILoggingBuilder Logging => InnerBuilder.Logging;

    public IMetricsBuilder Metrics => InnerBuilder.Metrics;

    public IServiceCollection Services => InnerBuilder.Services;
}
