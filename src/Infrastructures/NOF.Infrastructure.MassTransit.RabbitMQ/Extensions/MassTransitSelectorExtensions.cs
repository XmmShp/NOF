using MassTransit;
using Microsoft.Extensions.Configuration;
using NOF.Contract;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.MassTransit.RabbitMQ;

public static partial class NOFInfrastructureMassTransitRabbitMQExtensions
{
    extension(MassTransitSelector selector)
    {
        public INOFAppBuilder UseRabbitMQ(string connectStringName = "rabbitmq")
        {
            var nameProvider = selector.Builder.EndpointNameProvider;
            ArgumentNullException.ThrowIfNull(nameProvider);
            selector.Builder.RequestSender = new MassTransitRabbitMQStartupRequestRider(selector.Builder, connectStringName, nameProvider);
            selector.Builder.StartupEventChannel.Subscribe<MassTransitConfiguring>(e =>
            {
                var config = e.Configurator;
                config.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Publish<IRequestBase>(p => p.Exclude = true);
                    cfg.Publish<IRequest>(p => p.Exclude = true);
                    cfg.Publish<ICommand>(p => p.Exclude = true);
                    cfg.Publish<INotification>(p => p.Exclude = true);

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

public class MassTransitRabbitMQStartupRequestRider : IRequestRider
{
    private readonly INOFAppBuilder _builder;
    private readonly IEndpointNameProvider _nameProvider;
    private readonly string _connectStringName;

    public MassTransitRabbitMQStartupRequestRider(INOFAppBuilder builder, string connectStringName, IEndpointNameProvider nameProvider)
    {
        _builder = builder;
        _connectStringName = connectStringName;
        _nameProvider = nameProvider;
    }

    private IBusControl GetBusControl()
    {
        var connectString = _builder.Configuration.GetConnectionString(_connectStringName);
        ArgumentException.ThrowIfNullOrEmpty(connectString);

        var bus = Bus.Factory.CreateUsingRabbitMq(config => config.Host(new Uri(connectString)));
        return bus;
    }

    public async Task<Result> SendAsync(IRequest request, IDictionary<string, string?>? headers = null, string? destinationEndpointName = null, CancellationToken cancellationToken = default)
    {
        destinationEndpointName ??= _nameProvider.GetEndpointName(request.GetType());
        var bus = GetBusControl();
        await bus.StartAsync(cancellationToken);
        var result = await bus.SendAsync(request, destinationEndpointName.ToQueueUri(), cancellationToken);
        await bus.StopAsync(cancellationToken);
        return result;
    }

    public async Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, IDictionary<string, string?>? headers = null, string? destinationEndpointName = null,
        CancellationToken cancellationToken = default)
    {
        destinationEndpointName ??= _nameProvider.GetEndpointName(request.GetType());
        var bus = GetBusControl();
        await bus.StartAsync(cancellationToken);
        var result = await bus.SendAsync(request, destinationEndpointName.ToQueueUri(), cancellationToken);
        await bus.StopAsync(cancellationToken);
        return result;
    }
}
