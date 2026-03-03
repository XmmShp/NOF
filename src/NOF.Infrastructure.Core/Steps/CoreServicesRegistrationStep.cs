using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Infrastructure.Abstraction;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers core framework services including invocation context, command sender,
/// notification publisher, and endpoint name provider.
/// </summary>
public class CoreServicesRegistrationStep : IBaseSettingsServiceRegistrationStep<CoreServicesRegistrationStep>
{
    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode", Justification = "BindConfiguration is intercepted by EnableConfigurationBindingGenerator")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "BindConfiguration is intercepted by EnableConfigurationBindingGenerator")]
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.AddScoped<IInvocationContextInternal, InvocationContext>();
        builder.Services.AddScoped<IInvocationContext>(sp => sp.GetRequiredService<IInvocationContextInternal>());

        builder.Services.AddScoped<ICommandSender, CommandSender>();
        builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();
        builder.Services.AddScoped<IRequestSender, RequestSender>();
        builder.Services.AddScoped<IEventPublisher, InMemoryEventPublisher>();

        builder.Services.AddOptions<EndpointNameOptions>();
        builder.Services.AddSingleton<IEndpointNameProvider, ManualEndpointNameProvider>();

        builder.Services.AddOptions<OutboxOptions>()
            .BindConfiguration("NOF:Outbox")
            .ValidateOnStart();

        // Handler inbound pipeline: executor
        builder.Services.AddScoped<IInboundPipelineExecutor, InboundPipelineExecutor>();

        // Outbound pipeline: executor
        builder.Services.AddScoped<IOutboundPipelineExecutor, OutboundPipelineExecutor>();

        // State machine registry
        builder.Services.AddSingleton<IStateMachineRegistry, StateMachineRegistry>();

        return ValueTask.CompletedTask;
    }
}
