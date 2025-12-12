using MassTransit;
using Microsoft.Extensions.Configuration;

namespace NOF;

public static partial class __NOF_Infrastructure_MassTransit_RabbitMQ_Extensions__
{
    extension(INOFMassTransitSelector selector)
    {
        public INOFApp UseRabbitMQ(string connectStringName = "rabbitmq")
        {
            selector.App.CommandSender = new MassTransitRabbitMQStartupCommandSender(selector.App, connectStringName);
            selector.App.AddRegistrationConfigurator(new MassTransitRabbitMQConfigurator(connectStringName));
            return selector.App;
        }
    }
}

public class MassTransitRabbitMQStartupCommandSender : ICommandSender
{
    private readonly INOFApp _app;
    private readonly string _connectStringName;

    public MassTransitRabbitMQStartupCommandSender(INOFApp app, string connectStringName)
    {
        _app = app;
        _connectStringName = connectStringName;
    }

    public async Task<Result> SendAsync(ICommand command, string? destinationEndpointName, CancellationToken cancellationToken)
    {
        destinationEndpointName ??= command.GetType().GetEndpointName();
        var bus = GetBusControl();
        await bus.StartAsync(cancellationToken);
        var result = await bus.SendAsync(command, destinationEndpointName.ToQueueUri(), cancellationToken);
        await bus.StopAsync(cancellationToken);
        return result;
    }

    public async Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, string? destinationEndpointName, CancellationToken cancellationToken)
    {
        destinationEndpointName ??= command.GetType().GetEndpointName();
        var bus = GetBusControl();
        await bus.StartAsync(cancellationToken);
        var result = await bus.SendAsync(command, destinationEndpointName.ToQueueUri(), cancellationToken);
        await bus.StopAsync(cancellationToken);
        return result;
    }

    public async Task SendAsync(IAsyncCommand command, string? destinationEndpointName, CancellationToken cancellationToken)
    {
        destinationEndpointName ??= command.GetType().GetEndpointName();
        var bus = GetBusControl();
        await bus.StartAsync(cancellationToken);
        await bus.SendCommand(command, destinationEndpointName.ToQueueUri(), cancellationToken);
        await bus.StopAsync(cancellationToken);
    }

    private IBusControl GetBusControl()
    {
        var connectString = _app.Unwrap().Configuration.GetConnectionString(_connectStringName);
        ArgumentException.ThrowIfNullOrEmpty(connectString);

        var bus = Bus.Factory.CreateUsingRabbitMq(config => config.Host(new Uri(connectString)));
        return bus;
    }
}
