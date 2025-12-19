using MassTransit;
using Microsoft.Extensions.Configuration;

namespace NOF;

public static partial class __NOF_Infrastructure_MassTransit_RabbitMQ_Extensions__
{
    extension(INOFMassTransitSelector selector)
    {
        public INOFAppBuilder UseRabbitMQ(string connectStringName = "rabbitmq")
        {
            selector.Builder.RequestSender = new MassTransitRabbitMQStartupRequestSender(selector.Builder, connectStringName);
            selector.Builder.EventDispatcher.Subscribe<MassTransitConfiguring>(e =>
            {
                var config = e.Configurator;
                config.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Publish<IRequestBase>(p => p.Exclude = true);
                    cfg.Publish<IRequest>(p => p.Exclude = true);
                    cfg.Publish<ICommand>(p => p.Exclude = true);
                    cfg.Publish<INotification>(p => p.Exclude = true);

                    cfg.UseSendFilter(typeof(CorrelationFilter<>), context);
                    cfg.UsePublishFilter(typeof(CorrelationFilter<>), context);

                    var connectString = selector.Builder.Configuration.GetConnectionString(connectStringName);
                    ArgumentException.ThrowIfNullOrEmpty(connectString);
                    cfg.Host(new Uri(connectString));
                    cfg.ConfigureEndpoints(context);
                });
            });
            return selector.Builder;
        }
    }
}

public class MassTransitRabbitMQStartupRequestSender : IRequestSender
{
    private readonly INOFAppBuilder _builder;
    private readonly string _connectStringName;

    public MassTransitRabbitMQStartupRequestSender(INOFAppBuilder builder, string connectStringName)
    {
        _builder = builder;
        _connectStringName = connectStringName;
    }

    private IBusControl GetBusControl()
    {
        var connectString = _builder.Configuration.GetConnectionString(_connectStringName);
        ArgumentException.ThrowIfNullOrEmpty(connectString);

        var bus = Bus.Factory.CreateUsingRabbitMq(config => config.Host(new Uri(connectString)));
        return bus;
    }

    public async Task<Result> SendAsync(IRequest request, string? destinationEndpointName = null, CancellationToken cancellationToken = default)
    {
        destinationEndpointName ??= request.GetType().GetEndpointName();
        var bus = GetBusControl();
        await bus.StartAsync(cancellationToken);
        var result = await bus.SendAsync(request, destinationEndpointName.ToQueueUri(), cancellationToken);
        await bus.StopAsync(cancellationToken);
        return result;
    }

    public async Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, string? destinationEndpointName = null,
        CancellationToken cancellationToken = default)
    {
        destinationEndpointName ??= request.GetType().GetEndpointName();
        var bus = GetBusControl();
        await bus.StartAsync(cancellationToken);
        var result = await bus.SendAsync(request, destinationEndpointName.ToQueueUri(), cancellationToken);
        await bus.StopAsync(cancellationToken);
        return result;
    }
}