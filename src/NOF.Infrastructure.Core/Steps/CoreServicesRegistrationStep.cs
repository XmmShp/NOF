using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers core framework services including invocation context, command sender,
/// notification publisher, and endpoint name provider.
/// </summary>
public class CoreServicesRegistrationStep : IBaseSettingsServiceRegistrationStep
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.AddScoped<IInvocationContextInternal, InvocationContext>();
        builder.Services.AddScoped<IInvocationContext>(sp => sp.GetRequiredService<IInvocationContextInternal>());

        builder.Services.AddScoped<ICommandSender, CommandSender>();
        builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();
        builder.Services.AddScoped<IRequestSender, RequestSender>();

        builder.Services.AddSingleton<IEndpointNameProvider>(new EndpointNameProvider());

        builder.Services.AddOptionsInConfiguration<OutboxOptions>("NOF:Outbox");

        // Handler inbound pipeline: executor
        builder.Services.AddScoped<IInboundPipelineExecutor, InboundPipelineExecutor>();

        // Outbound pipeline: executor
        builder.Services.AddScoped<IOutboundPipelineExecutor, OutboundPipelineExecutor>();

        // State machine registry
        builder.Services.AddSingleton<IStateMachineRegistry, StateMachineRegistry>();

        return ValueTask.CompletedTask;
    }
}
