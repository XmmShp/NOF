using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace NOF.Hosting.Console;

/// <summary>
/// Console host builder for NOF, built on top of <see cref="HostApplicationBuilder"/>.
/// </summary>
public sealed class NOFConsoleHostBuilder : IHostApplicationBuilder
{
    public HostApplicationBuilder HostApplicationBuilder { get; }

    private NOFConsoleHostBuilder(string[]? args, Assembly? applicationAssembly)
    {
        HostApplicationBuilder = Host.CreateApplicationBuilder(args ?? []);
        if (applicationAssembly is not null)
        {
            this.AddApplicationPart(applicationAssembly);
        }
    }

    /// <summary>
    /// Creates a console NOF builder.
    /// </summary>
    /// <param name="args">Program arguments.</param>
    public static NOFConsoleHostBuilder Create(string[]? args = null)
    {
        var builder = new NOFConsoleHostBuilder(args, Assembly.GetCallingAssembly());
        builder.AddNOFInfrastructure();
        return builder;
    }

    public Task<IHost> BuildAsync()
        => this.BuildNOFAsync(HostApplicationBuilder.Build);

    public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull
        => HostApplicationBuilder.ConfigureContainer(factory, configure);

    public IDictionary<object, object> Properties => ((IHostApplicationBuilder)HostApplicationBuilder).Properties;

    public IConfigurationManager Configuration => HostApplicationBuilder.Configuration;

    public IHostEnvironment Environment => HostApplicationBuilder.Environment;

    public ILoggingBuilder Logging => HostApplicationBuilder.Logging;

    public IMetricsBuilder Metrics => HostApplicationBuilder.Metrics;

    public IServiceCollection Services => HostApplicationBuilder.Services;
}
