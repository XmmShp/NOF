using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NOF.Contract;
using NOF.Infrastructure.Core;
using System.Text.Json;

namespace NOF.Hosting.BlazorWebAssembly;

public class NOFWebAssemblyHostBuilder : NOFAppBuilder<NOFWebAssemblyHost>
{
    private readonly IConfigurationManager _configuration = new ConfigurationManager();

    private readonly WebAssemblyHostBuilder _innerBuilder;

    protected NOFWebAssemblyHostBuilder(string[]? args)
    {
        _innerBuilder = WebAssemblyHostBuilder.CreateDefault(args);
        Environment = new NOFWebAssemblyHostEnvironment(_innerBuilder.HostEnvironment);
        _configuration.AddConfiguration(_innerBuilder.Configuration);
    }

    public static NOFWebAssemblyHostBuilder Create(string[]? args)
    {
        var builder = new NOFWebAssemblyHostBuilder(args);
        builder.AddRegistrationStep(new BrowserStorageRegistrationStep());
        JsonSerializerOptions.ConfigureNOFJsonSerializerOptions(options =>
        {
            options.TypeInfoResolverChain.Add(NOFWebAssemblyJsonContext.Default);
        });
        return builder;
    }

    protected override Task<NOFWebAssemblyHost> BuildApplicationAsync()
        => Task.FromResult(new NOFWebAssemblyHost(_innerBuilder.Build()));

    public override void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
        => _innerBuilder.ConfigureContainer(factory, configure);

    public override IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();

    public override IConfigurationManager Configuration => _configuration;

    public override IHostEnvironment Environment { get; }

    public override ILoggingBuilder Logging => _innerBuilder.Logging;

    public override IMetricsBuilder Metrics => field ??= InitializeMetrics();

    public override IServiceCollection Services => _innerBuilder.Services;

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
