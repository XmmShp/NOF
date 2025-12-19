using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NOF;

public static partial class __NOF_Infrastructure_MassTransit_Extensions__
{
    extension(INOFAppBuilder builder)
    {
        public INOFMassTransitSelector AddMassTransit<THostApplicationBuilder>(INOFAppBuilder<THostApplicationBuilder> originBuilder)
            where THostApplicationBuilder : class, IHost
        {
            builder.Services.AddScoped<IRequestHandleNodeFactory, RequestHandleNodeFactory>();
            builder.Services.AddScoped<ICommandSender, MassTransitCommandSender>();
            builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
            builder.Services.AddScoped<INotificationPublisher, MassTransitNotificationPublisher>();
            builder.Services.AddScoped<IRequestSender, MassTransitRequestSender>();
            builder.AddServiceConfig(new MassTransitConfig());

            builder.Services.AddSingleton<IRequestHandleNodeRegistry, RequestHandleNodeRegistry>();
            var selector = new NOFMassTransitSelector(builder);
            selector.AddRequestHandleNode(originBuilder, typeof(RiderRequestHandleNode));
            selector.AddRequestHandleNode(originBuilder, typeof(MediatorRequestHandleNode));
            return selector;
        }
    }
}
