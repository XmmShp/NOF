using MassTransit.Logging;
using MassTransit.Monitoring;
using Microsoft.Extensions.DependencyInjection;

namespace NOF;

public static partial class __NOF_Infrastructure_MassTransit_Extensions__
{
    extension(INOFAppBuilder builder)
    {
        public INOFMassTransitSelector AddMassTransit()
        {
            builder.ActivitySources.Add(DiagnosticHeaders.DefaultListenerName);
            builder.MetricNames.Add(InstrumentationOptions.MeterName);

            builder.Services.AddScoped<IRequestHandleNodeFactory, RequestHandleNodeFactory>();
            builder.Services.AddScoped<ICommandSender, MassTransitCommandSender>();
            builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
            builder.Services.AddScoped<INotificationPublisher, MassTransitNotificationPublisher>();
            builder.Services.AddScoped<IRequestSender, MassTransitRequestSender>();
            builder.AddRegistrationStep(new MassTransitRegistrationStep());

            builder.Services.AddSingleton<IRequestHandleNodeRegistry, RequestHandleNodeRegistry>();
            var selector = new NOFMassTransitSelector(builder);
            selector.AddRequestHandleNode(typeof(RiderRequestHandleNode));
            selector.AddRequestHandleNode(typeof(MediatorRequestHandleNode));
            return selector;
        }
    }
}
