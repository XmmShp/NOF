using Microsoft.Extensions.DependencyInjection;
using NOF.Contract;
using NOF.Hosting;

namespace NOF.Infrastructure.RabbitMQ;

public static partial class NOFInfrastructureRabbitMQExtensions
{
    public static INOFAppBuilder AddRabbitMQ(this INOFAppBuilder builder)
        => AddRabbitMQ(builder, _ => { });

    public static INOFAppBuilder AddRabbitMQ(this INOFAppBuilder builder, Action<RabbitMQOptions> configureOptions)
    {
        builder.Services.Configure(configureOptions);

        builder.Services.ReplaceOrAddSingleton<RabbitMQConnectionManager, RabbitMQConnectionManager>();
        builder.Services.ReplaceOrAddScoped<ICommandRider, RabbitMQCommandRider>();
        builder.Services.ReplaceOrAddScoped<INotificationRider, RabbitMQNotificationRider>();

        // 注册应用启动时的消费者初始化
        builder.Services.AddHostedService<RabbitMQConsumerHostedService>();

        return builder;
    }
}
