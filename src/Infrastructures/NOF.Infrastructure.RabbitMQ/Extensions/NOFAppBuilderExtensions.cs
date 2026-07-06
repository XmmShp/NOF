using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Application;
using NOF.Infrastructure;
using NOF.Infrastructure.RabbitMQ;

namespace NOF.Hosting;

public static partial class NOFInfrastructureRabbitMQExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddRabbitMQBackplane()
        {
            builder.Services.ReplaceOrAddSingleton<RabbitMQConnectionManager, RabbitMQConnectionManager>();
            builder.Services.ReplaceOrAddSingleton<IBackplane, RabbitMQBackplane>();

            return builder;
        }

        public IHostApplicationBuilder AddRabbitMQBackplane(Action<RabbitMQOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);

            builder.Services.Configure(configureOptions);
            return builder.AddRabbitMQBackplane();
        }

        public IHostApplicationBuilder AddRabbitMQ(Action<RabbitMQOptions> configureOptions)
        {
            builder.Services.Configure(configureOptions);

            builder.Services.ReplaceOrAddSingleton<RabbitMQConnectionManager, RabbitMQConnectionManager>();
            builder.Services.ReplaceOrAddSingleton<ICommandRider, RabbitMQCommandRider>();
            builder.Services.ReplaceOrAddSingleton<INotificationRider, RabbitMQNotificationRider>();

            // 注册应用启动时的消费者初始化
            builder.Services.AddHostedService<RabbitMQConsumerHostedService>();

            return builder;
        }
    }
}
