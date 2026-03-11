using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using NOF.Infrastructure.Abstraction;
using NOF.Infrastructure.Core;

namespace NOF.Test;

public sealed class NOFTestAppBuilder : NOFAppBuilder<IHost>
{
    public HostApplicationBuilder InnerBuilder { get; }

    private NOFTestAppBuilder(string[]? args)
    {
        InnerBuilder = Host.CreateApplicationBuilder(args ?? []);
        ServiceConfigs.Clear();
        ApplicationConfigs.Clear();
        ConfigureDefaultTestServices();
    }

    public static NOFTestAppBuilder Create(string[]? args = null)
    {
        return new NOFTestAppBuilder(args);
    }

    public async Task<NOFTestHost> BuildTestHostAsync()
    {
        var host = await BuildAsync();
        IdGenerator.SetCurrent(host.Services.GetRequiredService<IIdGenerator>());
        Mapper.SetCurrent(host.Services.GetRequiredService<IMapper>());
        return new NOFTestHost(host);
    }

    private void ConfigureDefaultTestServices()
    {
        Services.AddOptions();
        Services.TryAddSingleton(Options.Create(new MapperOptions()));
        Services.TryAddSingleton(Options.Create(new SnowflakeIdGeneratorOptions()));

        Services.TryAddSingleton<IMapper>(sp => new ManualMapper(sp.GetRequiredService<IOptions<MapperOptions>>()));
        Services.TryAddSingleton<IIdGenerator>(sp =>
            new SnowflakeIdGenerator(sp.GetRequiredService<IOptions<SnowflakeIdGeneratorOptions>>().Value));

        Services.TryAddSingleton<InboundPipelineTypes>();
        Services.TryAddSingleton<OutboundPipelineTypes>();

        new CoreServicesRegistrationStep().ExecuteAsync(this).GetAwaiter().GetResult();
        new OutboxRegistrationStep().ExecuteAsync(this).GetAwaiter().GetResult();
    }

    protected override Task<IHost> BuildApplicationAsync()
    {
        return Task.FromResult(InnerBuilder.Build());
    }

    public override void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
    {
        InnerBuilder.ConfigureContainer(factory, configure);
    }

    public override IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();

    public override IConfigurationManager Configuration => InnerBuilder.Configuration;

    public override IHostEnvironment Environment => InnerBuilder.Environment;

    public override ILoggingBuilder Logging => InnerBuilder.Logging;

    public override IMetricsBuilder Metrics => InnerBuilder.Metrics;

    public override IServiceCollection Services => InnerBuilder.Services;
}
