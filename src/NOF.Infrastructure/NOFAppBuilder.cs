using MassTransit;
using MassTransit.Internals;
using MassTransit.Metadata;
using MassTransit.Util;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Yitter.IdGenerator;

[assembly: InternalsVisibleTo("NOF.Infrastructure.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]

namespace NOF;

public class NOFAppBuilder
{
    private readonly List<Action<WebApplicationBuilder>> _buildActions;
    private readonly List<Action<IConsumePipeConfigurator, IRegistrationContext>> _configureConsumePipeActions;
    private readonly Dictionary<string, object?> _metadata;
    private readonly List<Action<WebApplication>> _appConfigurators;

    public WebApplicationBuilder Builder { get; }
    public List<Assembly> Assemblies { get; }

    public IServiceCollection Services => Builder.Services;

    public string RabbitMQConnectionString
    {
        get => Builder.Configuration.GetConnectionString("rabbitmq") ?? string.Empty;
        set => Builder.Configuration.GetSection("ConnectionStrings")["rabbitmq"] = value;
    }

    public string[] Args { get; }

    public NOFAppBuilder SetMetadata(string name, object? value)
    {
        _metadata[name] = value;
        return this;
    }

    public T? GetMetadata<T>(string name)
    {
        if (_metadata.TryGetValue(name, out var value)
            && value is T typedValue)
        {
            return typedValue;
        }

        return default;
    }

    public T GetOrSetMetadata<T>(string name, Func<T> valueFactory)
    {
        if (_metadata.TryGetValue(name, out var value)
            && value is T typedValue)
        {
            return typedValue;
        }

        var defaultValue = valueFactory();
        SetMetadata(name, defaultValue);
        return defaultValue;
    }

    internal NOFAppBuilder(string[] args)
    {
        _buildActions = [];
        _metadata = [];
        _configureConsumePipeActions = [];
        _appConfigurators = [];
        Args = args;
        Assemblies = [];
        Builder = WebApplication.CreateBuilder(args);
    }

    public static NOFAppBuilder CreateApp(string[] args)
    {
        var app = new NOFAppBuilder(args);

        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        app.Assemblies.Add(assembly);

        app.AddAction(ConfigureJsonOptions);
        app.AddAction(app.AddMassTransit);
        app.AddAction(app.ConfigureServices);
        return app;
    }

    private static void ConfigureJsonOptions(WebApplicationBuilder builder)
    {
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            options.SerializerOptions.Converters.Add(OptionalConverterFactory.Instance);
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });
    }

    public NOFAppBuilder AddAction(Action<WebApplicationBuilder> buildAction)
    {
        _buildActions.Add(buildAction);
        return this;
    }

    public NOFAppBuilder ConfigureConsumePipe(Action<IConsumePipeConfigurator, IRegistrationContext> configuration)
    {
        _configureConsumePipeActions.Add(configuration);
        return this;
    }

    public NOFAppBuilder ConfigureApp(Action<WebApplication> configuration)
    {
        _appConfigurators.Add(configuration);
        return this;
    }

    public WebApplication Build()
    {
        foreach (var builtAction in _buildActions)
        {
            builtAction(Builder);
        }
        var app = Builder.Build();
        foreach (var configurator in _appConfigurators)
        {
            configurator(app);
        }

        return app;
    }

    private void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ICommandSender, CommandSender>();
        builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();

        builder.Services.AddScoped<ApiResponseMiddleware>();
        ConfigureApp(app =>
        {
            app.UseMiddleware<ApiResponseMiddleware>();
        });

        builder.Services.AddOptionsInConfiguration<IdGeneratorOptions>();
        ConfigureApp(app =>
        {
            var idGeneratorOptions = app.Services.GetRequiredService<IOptions<IdGeneratorOptions>>().Value;
            YitIdHelper.SetIdGenerator(idGeneratorOptions);
        });
    }

    private void AddMassTransit(WebApplicationBuilder builder)
    {
        var consumers = AssemblyTypeCache.FindTypes(Assemblies,
            TypeClassification.Concrete | TypeClassification.Closed,
            RegistrationMetadata.IsConsumerOrDefinition
        ).GetAwaiter().GetResult().ToList();

        var internals = consumers.Where(t => t.HasInterface<IRequestHandler>() || t.HasInterface<IEventHandler>());
        var externals = consumers.Where(t => t.HasInterface<ICommandHandler>() || t.HasInterface<INotificationHandler>());

        builder.Services.AddMediator(config =>
        {
            config.AddConsumers(internals.ToArray());
            config.ConfigureMediator((context, cfg) =>
            {
                foreach (var pipeAction in _configureConsumePipeActions)
                {
                    pipeAction(cfg, context);
                }
            });
        });

        builder.Services.AddMassTransit(config =>
        {
            config.SetEndpointNameFormatter(EndpointNameFormatter.Instance);
            config.AddConsumers(externals.ToArray());
            config.UsingRabbitMq((context, cfg) =>
            {
                var connectionString = RabbitMQConnectionString ?? throw new InvalidOperationException();
                cfg.Host(new Uri(connectionString));
                cfg.ConfigureEndpoints(context);

                foreach (var pipeAction in _configureConsumePipeActions)
                {
                    pipeAction(cfg, context);
                }
            });
        });
    }
}