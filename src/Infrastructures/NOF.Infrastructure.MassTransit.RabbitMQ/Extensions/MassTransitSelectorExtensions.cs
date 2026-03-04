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
            selector.ConfigureBus(config =>
            {
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
