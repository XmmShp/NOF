using MassTransit.Logging;
using MassTransit.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Infrastructure.Core;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace NOF.Infrastructure.MassTransit;

public static partial class NOFInfrastructureMassTransitExtensions
{
    extension(INOFAppBuilder builder)
    {
        public MassTransitSelector AddMassTransit()
        {
            builder.Services.ConfigureOpenTelemetryMeterProvider(meter => meter.AddMeter(InstrumentationOptions.MeterName));
            builder.Services.ConfigureOpenTelemetryTracerProvider(tracer => tracer.AddSource(DiagnosticHeaders.DefaultListenerName));

            builder.Services.AddScoped<IRequestHandleNodeFactory, RequestHandleNodeFactory>();
            builder.Services.AddScoped<ICommandRider, MassTransitCommandRider>();
            builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
            builder.Services.AddScoped<INotificationRider, MassTransitNotificationRider>();
            builder.Services.AddScoped<IRequestSender, MassTransitRequestSender>();
            builder.AddRegistrationStep(new MassTransitRegistrationStep());

            builder.Services.AddSingleton<IRequestHandleNodeRegistry, RequestHandleNodeRegistry>();
            var selector = new MassTransitSelector(builder);
            selector.AddRequestHandleNode(typeof(RiderRequestHandleNode));
            selector.AddRequestHandleNode(typeof(MediatorRequestHandleNode));
            return selector;
        }
    }
}
