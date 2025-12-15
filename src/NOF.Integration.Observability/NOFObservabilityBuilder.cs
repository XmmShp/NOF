using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NOF;

public interface INOFObservableAppBuilder : INOFAppBuilder;

internal class NOFObservableAppBuilder : INOFObservableAppBuilder
{
    private readonly INOFAppBuilder _builder;
    public NOFObservableAppBuilder(INOFAppBuilder builder)
    {
        _builder = builder;
    }

    public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory,
        Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull
        => _builder.ConfigureContainer(factory, configure);

    public IDictionary<object, object> Properties => _builder.Properties;
    public IConfigurationManager Configuration => _builder.Configuration;
    public IHostEnvironment Environment => _builder.Environment;
    public ILoggingBuilder Logging => _builder.Logging;
    public IMetricsBuilder Metrics => _builder.Metrics;
    public IServiceCollection Services => _builder.Services;
    public INOFAppBuilder AddServiceConfig(IServiceConfig config) => _builder.AddServiceConfig(config);
    public INOFAppBuilder RemoveServiceConfig(Predicate<IServiceConfig> predicate) => _builder.RemoveServiceConfig(predicate);
    public IEventDispatcher EventDispatcher => _builder.EventDispatcher;
    public IRequestSender? RequestSender
    {
        get => _builder.RequestSender;
        set => _builder.RequestSender = value;
    }
}
