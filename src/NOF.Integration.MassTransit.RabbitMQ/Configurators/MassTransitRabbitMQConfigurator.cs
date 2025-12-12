using MassTransit;
using Microsoft.Extensions.Configuration;

namespace NOF;

public class MassTransitRabbitMQConfigurator : IMassTransitConfiguring, IConfiguredServicesConfigurator
{
    private readonly string _connectStringName;
    public MassTransitRabbitMQConfigurator(string connectStringName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectStringName);
        _connectStringName = connectStringName;
    }
    public ValueTask ExecuteAsync(INOFApp app)
    {
        EventDispatcher.Subscribe<MassTransitConfiguring>(e =>
        {
            var config = e.Configurator;
            config.UsingRabbitMq((context, cfg) =>
            {
                cfg.Publish<ICommandBase>(p => p.Exclude = true);
                cfg.Publish<ICommand>(p => p.Exclude = true);
                cfg.Publish<IAsyncCommand>(p => p.Exclude = true);
                cfg.Publish<INotification>(p => p.Exclude = true);

                var connectString = app.Unwrap().Configuration.GetConnectionString(_connectStringName);
                ArgumentException.ThrowIfNullOrEmpty(connectString);
                cfg.Host(new Uri(connectString));
                cfg.ConfigureEndpoints(context);
            });
        });
        return ValueTask.CompletedTask;
    }
}
