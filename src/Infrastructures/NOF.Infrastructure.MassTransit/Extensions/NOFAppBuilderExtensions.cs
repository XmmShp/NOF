using MassTransit.Logging;
using MassTransit.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using NOF.Infrastructure.Abstraction;
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

            builder.Services.AddScoped<ICommandRider, MassTransitCommandRider>();
            builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
            builder.Services.AddScoped<INotificationRider, MassTransitNotificationRider>();
            builder.Services.AddScoped<IRequestRider, MassTransitRequestRider>();
            builder.AddRegistrationStep(new MassTransitRegistrationStep());

            return new MassTransitSelector(builder);
        }
    }
}
