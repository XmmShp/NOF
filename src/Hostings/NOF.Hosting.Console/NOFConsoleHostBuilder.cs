using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Infrastructure;
using System.Reflection;

namespace NOF.Hosting.Console;

/// <summary>
/// Console host builder for NOF, built on top of <see cref="HostApplicationBuilder"/>.
/// </summary>
public sealed class NOFConsoleHostBuilder : NOFAppBuilder<IHost>
{
    public HostApplicationBuilder HostApplicationBuilder { get; }

    private NOFConsoleHostBuilder(string[]? args, Assembly? applicationAssembly)
        : base(applicationAssembly)
    {
        HostApplicationBuilder = Host.CreateApplicationBuilder(args ?? []);
    }

    /// <summary>
    /// Creates a console NOF builder.
    /// </summary>
    /// <param name="args">Program arguments.</param>
    /// <param name="useInfrastructureDefaults">When true, calls <see cref="NOFAppBuilderExtensions.AddInfrastructureDefaults"/>.</param>
    public static NOFConsoleHostBuilder Create(string[]? args = null, bool useInfrastructureDefaults = true)
    {
        var builder = new NOFConsoleHostBuilder(args, Assembly.GetCallingAssembly());
        if (useInfrastructureDefaults)
        {
            builder.AddInfrastructureDefaults();
        }

        return builder;
    }

    protected override Task<IHost> BuildApplicationAsync()
        => Task.FromResult(HostApplicationBuilder.Build());

    public override void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
        => HostApplicationBuilder.ConfigureContainer(factory, configure);

    public override IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();

    public override IConfigurationManager Configuration => HostApplicationBuilder.Configuration;

    public override IHostEnvironment Environment => HostApplicationBuilder.Environment;

    public override ILoggingBuilder Logging => HostApplicationBuilder.Logging;

    public override IMetricsBuilder Metrics => HostApplicationBuilder.Metrics;

    public override IServiceCollection Services => HostApplicationBuilder.Services;
}
