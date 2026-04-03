using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NOF.Hosting;

namespace NOF.Infrastructure.RabbitMQ;

public static partial class NOFInfrastructureRabbitMQExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddRabbitMQ(string? connectionStringName = null)
            => AddRabbitMQ(builder, options =>
            {
                var connectionString = builder.Configuration.GetConnectionString(connectionStringName ?? "rabbitmq");
                if (!string.IsNullOrEmpty(connectionString))
                {
                    options.ConnectionString = connectionString;
                }
            });

        public INOFAppBuilder AddRabbitMQ(Action<RabbitMQOptions> configureOptions)
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
