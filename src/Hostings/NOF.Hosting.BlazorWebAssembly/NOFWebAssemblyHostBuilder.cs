using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Contract;
using NOF.Infrastructure;
using NOF.UI;
using System.Text.Json;

namespace NOF.Hosting.BlazorWebAssembly;

public class NOFWebAssemblyHostBuilder : NOFAppBuilder<NOFWebAssemblyHost>
{
    private readonly IConfigurationManager _configuration = new ConfigurationManager();

    public WebAssemblyHostBuilder WebAssemblyHostBuilder { get; }

    protected NOFWebAssemblyHostBuilder(string[]? args)
    {
        WebAssemblyHostBuilder = WebAssemblyHostBuilder.CreateDefault(args);
        Environment = new NOFWebAssemblyHostEnvironment(WebAssemblyHostBuilder.HostEnvironment);
        _configuration.AddConfiguration(WebAssemblyHostBuilder.Configuration);
    }

    public static NOFWebAssemblyHostBuilder Create(string[]? args)
    {
        var builder = new NOFWebAssemblyHostBuilder(args);
        builder.AddRegistrationStep(new BrowserStorageRegistrationStep());
        JsonSerializerOptions.ConfigureNOFJsonSerializerOptions(options =>
        {
            options.TypeInfoResolverChain.Add(NOFUIJsonContext.Default);
        });
        return builder;
    }

    protected override Task<NOFWebAssemblyHost> BuildApplicationAsync()
        => Task.FromResult(new NOFWebAssemblyHost(WebAssemblyHostBuilder.Build()));

    public override void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
        => WebAssemblyHostBuilder.ConfigureContainer(factory, configure);

    public override IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();

    public override IConfigurationManager Configuration => _configuration;

    public override IHostEnvironment Environment { get; }

    public override ILoggingBuilder Logging => WebAssemblyHostBuilder.Logging;

    public override IMetricsBuilder Metrics => field ??= InitializeMetrics();

    public override IServiceCollection Services => WebAssemblyHostBuilder.Services;

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
