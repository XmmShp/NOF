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
            services.AddOptions<RabbitMQOptions>()
                .Validate(
                    static options => !string.IsNullOrWhiteSpace(options.HostName),
                    "RabbitMQ HostName must be configured.")
                .Validate(
                    static options => options.Port >= 0,
                    "RabbitMQ Port must be greater than or equal to zero.")
                .Validate(
                    static options => options.PrefetchCount > 0,
                    "RabbitMQ PrefetchCount must be greater than zero.")
                .Validate(
                    static options => !options.PublisherConfirmationsEnabled || options.PublisherConfirmationTrackingEnabled,
                    "RabbitMQ publisher confirmation tracking must be enabled when publisher confirmations are enabled.")
                .ValidateOnStart();
            services.ReplaceOrAddSingleton<RabbitMQConnectionManager, RabbitMQConnectionManager>();
            services.ReplaceOrAddSingleton<ICommandRider, RabbitMQCommandRider>();
            services.ReplaceOrAddSingleton<INotificationRider, RabbitMQNotificationRider>();
            services.AddHostedService<RabbitMQConsumerHostedService>();
            return services;
        }
    }
}
