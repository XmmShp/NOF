using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Application;
using NOF.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

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

    public NOFTestAppBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(Services);
        return this;
    }

    public NOFTestAppBuilder AddApplicationPartOf<TMarker>()
    {
        Hosting.NOFHostingExtensions.AddApplicationPart(this, typeof(TMarker).Assembly);
        return this;
    }

    public NOFTestAppBuilder AddApplicationPart(Assembly assembly)
    {
        Hosting.NOFHostingExtensions.AddApplicationPart(this, assembly);
        return this;
    }

    public NOFTestAppBuilder AddRpcServer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRpcServer>()
        where TRpcServer : RpcServer, IRpcServer
    {
        Hosting.NOFInfrastructureExtensions.AddRpcServer<TRpcServer>(this);
        return this;
    }

    public NOFTestAppBuilder AddInMemoryPersistence()
    {
        Microsoft.Extensions.DependencyInjection.NOFInfrastructureExtensions.AddInMemoryPersistence(Services);
        return this;
    }

    public NOFTestAppBuilder AddLocalRpcClient<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        Services.ReplaceOrAddScoped<TService, TImplementation>();
        return this;
    }

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
