using Microsoft.Extensions.Hosting;

namespace NOF.Infrastructure;

public static partial class NOFInfrastructureExtensions
{
    extension(INOFAppBuilder builder)
    {
        /// <summary>
        /// Applies the built-in NOF infrastructure defaults (service registration steps and initialization steps).
        /// Each default step is added only when a step of the same type is not already present.
        /// </summary>
        public INOFAppBuilder AddInfrastructureDefaults()
        {
            builder.TryAddRegistrationStep<CoreServicesRegistrationStep>()
                .TryAddRegistrationStep<FallbackServiceRegistrationStep>()
                .TryAddRegistrationStep<OpenTelemetryRegistrationStep>()
                .TryAddRegistrationStep<ExceptionInboundMiddlewareStep>()
                .TryAddRegistrationStep<TenantInboundMiddlewareStep>()
                .TryAddRegistrationStep<AuthorizationInboundMiddlewareStep>()
                .TryAddRegistrationStep<TracingInboundMiddlewareStep>()
                .TryAddRegistrationStep<AutoInstrumentationInboundMiddlewareStep>()
                .TryAddRegistrationStep<MessageInboxInboundMiddlewareStep>()
                .TryAddRegistrationStep<HandlerKeyedServiceRegistrationStep>()
                .TryAddRegistrationStep<MessageIdOutboundMiddlewareStep>()
                .TryAddRegistrationStep<TracingOutboundMiddlewareStep>()
                .TryAddRegistrationStep<TenantOutboundMiddlewareStep>()
                .TryAddInitializationStep<IdGeneratorInitializationStep>()
                .TryAddInitializationStep<MapperInitializationStep>();

            return builder;
        }

        /// <summary>
        /// Adds a service configuration delegate that will be executed during the service registration phase.
        /// </summary>
        public INOFAppBuilder AddRegistrationStep(Func<IServiceRegistrationContext, ValueTask> func)
            => builder.AddRegistrationStep(new ServiceRegistrationStep(func));

        /// <summary>
        /// Adds an application configuration delegate that will be executed after the host is built but before it starts.
        /// </summary>
        public INOFAppBuilder AddInitializationStep(Func<IHostApplicationBuilder, IHost, Task> func)
            => builder.AddInitializationStep(new ApplicationInitializationStep(func));
    }
}
