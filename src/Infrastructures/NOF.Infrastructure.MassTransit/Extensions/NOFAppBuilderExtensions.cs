using MassTransit.Logging;
using MassTransit.Monitoring;
using NOF.Hosting;
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

            builder.Services.ReplaceOrAddScoped<ICommandRider, MassTransitCommandRider>();
            builder.Services.ReplaceOrAddScoped<INotificationRider, MassTransitNotificationRider>();
            var step = new MassTransitRegistrationStep();
            builder.AddRegistrationStep(step);

            return new MassTransitSelector(builder, step);
        }
    }
}
