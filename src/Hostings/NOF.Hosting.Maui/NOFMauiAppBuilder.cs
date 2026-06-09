using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace NOF.Hosting.Maui;

public class NOFMauiAppBuilder : NOFAppBuilder<NOFMauiApp>
{
    public MauiAppBuilder MauiAppBuilder { get; }

    protected NOFMauiAppBuilder(MauiAppBuilder mauiAppBuilder, Assembly? applicationAssembly)
    {
        MauiAppBuilder = mauiAppBuilder;
        Environment = new NOFMauiHostEnvironment(MauiAppBuilder.Environment);
        Services.Replace(ServiceDescriptor.Singleton(Environment));
        if (applicationAssembly is not null)
        {
            this.AddApplicationPart(applicationAssembly);
        }
    }

    public static NOFMauiAppBuilder Create(bool useDefaults = true)
    {
        return new NOFMauiAppBuilder(MauiApp.CreateBuilder(useDefaults), Assembly.GetCallingAssembly());
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
    public override IHostEnvironment Environment { get; }

    /// <inheritdoc />
    public override ILoggingBuilder Logging => MauiAppBuilder.Logging;

    /// <inheritdoc />
    public override IMetricsBuilder Metrics => ((IHostApplicationBuilder)MauiAppBuilder).Metrics;

    /// <inheritdoc />
    public override IServiceCollection Services => MauiAppBuilder.Services;

    private sealed class NOFMauiHostEnvironment(IHostEnvironment innerEnvironment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = GetValueOrDefault(
            () => innerEnvironment.EnvironmentName,
            Environments.Production);

        public string ApplicationName { get; set; } = GetValueOrDefault(
            () => innerEnvironment.ApplicationName,
            AppDomain.CurrentDomain.FriendlyName);

        public string ContentRootPath { get; set; } = GetValueOrDefault(
            () => innerEnvironment.ContentRootPath,
            AppContext.BaseDirectory);

        public IFileProvider ContentRootFileProvider { get; set; } = GetValueOrDefault(
            () => innerEnvironment.ContentRootFileProvider,
            new NullFileProvider());

        private static T GetValueOrDefault<T>(Func<T> valueFactory, T defaultValue)
        {
            try
            {
                return valueFactory() ?? defaultValue;
            }
            catch (NotImplementedException)
            {
                return defaultValue;
            }
        }
    }
}
