using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace NOF.Hosting.BlazorWebAssembly;

public class NOFWebAssemblyHostBuilder : IHostApplicationBuilder
{
    private readonly IConfigurationManager _configuration = new ConfigurationManager();
    private readonly Dictionary<object, object> _properties = [];

    public WebAssemblyHostBuilder WebAssemblyHostBuilder { get; }

    protected NOFWebAssemblyHostBuilder(string[]? args, Assembly? applicationAssembly)
    {
        WebAssemblyHostBuilder = WebAssemblyHostBuilder.CreateDefault(args);
        Environment = new NOFWebAssemblyHostEnvironment(WebAssemblyHostBuilder.HostEnvironment);
        _configuration.AddConfiguration(WebAssemblyHostBuilder.Configuration);
        if (applicationAssembly is not null)
        {
            this.AddApplicationPart(applicationAssembly);
        }
    }

    public static NOFWebAssemblyHostBuilder Create(string[]? args)
    {
        var builder = new NOFWebAssemblyHostBuilder(args, Assembly.GetCallingAssembly());
        builder.Services.AddNOFUI();
        return builder;
    }

    public Task<NOFWebAssemblyHost> BuildAsync()
        => this.BuildNOFAsync(() => new NOFWebAssemblyHost(WebAssemblyHostBuilder.Build()));

    public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull
        => WebAssemblyHostBuilder.ConfigureContainer(factory, configure);

    public IDictionary<object, object> Properties => _properties;

    public IConfigurationManager Configuration => _configuration;

    public IHostEnvironment Environment { get; }

    public ILoggingBuilder Logging => WebAssemblyHostBuilder.Logging;

    public IMetricsBuilder Metrics => field ??= InitializeMetrics();

    public IServiceCollection Services => WebAssemblyHostBuilder.Services;

    private IMetricsBuilder InitializeMetrics()
    {
        IMetricsBuilder? metrics = null;
        Services.AddMetrics(builder => metrics = builder);
        return metrics!;
    }

    private sealed class NOFWebAssemblyHostEnvironment(IWebAssemblyHostEnvironment innerEnvironment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = innerEnvironment.Environment;

        public string ApplicationName { get; set; } = AppDomain.CurrentDomain.FriendlyName;

        public string ContentRootPath { get; set; } = "/";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
