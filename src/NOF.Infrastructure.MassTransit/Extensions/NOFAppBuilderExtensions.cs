using MassTransit.Logging;
using MassTransit.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NOF;

public static partial class __NOF_Infrastructure_MassTransit_Extensions__
{
    extension<THostApplication>(INOFAppBuilder<THostApplication> builder)
        where THostApplication : class, IHost
    {
        public INOFMassTransitSelector<THostApplication> AddMassTransit()
        {
            builder.ActivitySources.Add(DiagnosticHeaders.DefaultListenerName);
            builder.MetricNames.Add(InstrumentationOptions.MeterName);

            builder.Services.AddScoped<IRequestHandleNodeFactory, RequestHandleNodeFactory>();
            builder.Services.AddScoped<ICommandSender, MassTransitCommandSender>();
            builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
            builder.Services.AddScoped<INotificationPublisher, MassTransitNotificationPublisher>();
            builder.Services.AddScoped<IRequestSender, MassTransitRequestSender>();
            builder.AddServiceConfig(new MassTransitConfig());

            builder.Services.AddSingleton<IRequestHandleNodeRegistry, RequestHandleNodeRegistry>();
            var selector = new NOFMassTransitSelector<THostApplication>(builder);
            selector.AddRequestHandleNode(typeof(RiderRequestHandleNode));
            selector.AddRequestHandleNode(typeof(MediatorRequestHandleNode));
            return selector;
        }
    }
}
