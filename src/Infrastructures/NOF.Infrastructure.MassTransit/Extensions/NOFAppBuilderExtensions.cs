using Microsoft.Extensions.DependencyInjection;

namespace NOF;

public static partial class __NOF_Infrastructure_MassTransit_Extensions__
{
    extension(INOFAppBuilder builder)
    {
        public INOFMassTransitSelector AddMassTransit()
        {
            /*
            builder.Services.ConfigureOpenTelemetryMeterProvider(meter => meter.AddMeter(InstrumentationOptions.MeterName));
            builder.Services.ConfigureOpenTelemetryTracerProvider(tracer => tracer.AddSource(DiagnosticHeaders.DefaultListenerName));
            */

            builder.Services.AddScoped<IRequestHandleNodeFactory, RequestHandleNodeFactory>();
            builder.Services.AddScoped<ICommandRider, MassTransitCommandRider>();
            builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
            builder.Services.AddScoped<INotificationRider, MassTransitNotificationRider>();
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
