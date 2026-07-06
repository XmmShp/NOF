using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Infrastructure;
using NOF.Infrastructure.RabbitMQ;

namespace NOF.Hosting;

public static partial class NOFInfrastructureRabbitMQExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRabbitMQBackplane()
        {
            services.ReplaceOrAddSingleton<RabbitMQConnectionManager, RabbitMQConnectionManager>();
            services.ReplaceOrAddSingleton<IBackplane, RabbitMQBackplane>();
            return services;
        }

        public IServiceCollection AddRabbitMQBackplane(Action<RabbitMQOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            services.Configure(configureOptions);
            return services.AddRabbitMQBackplane();
        }

        public IServiceCollection AddRabbitMQ(Action<RabbitMQOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            services.Configure(configureOptions);
            services.ReplaceOrAddSingleton<RabbitMQConnectionManager, RabbitMQConnectionManager>();
            services.ReplaceOrAddSingleton<ICommandRider, RabbitMQCommandRider>();
            services.ReplaceOrAddSingleton<INotificationRider, RabbitMQNotificationRider>();
            services.AddHostedService<RabbitMQConsumerHostedService>();
            return services;
        }
    }
}
