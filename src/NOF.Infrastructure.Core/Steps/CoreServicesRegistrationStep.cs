using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure.Abstraction;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers core framework services including invocation context, command sender,
/// notification publisher, handler resolvers, and handler endpoint name map.
/// </summary>
public class CoreServicesRegistrationStep : IBaseSettingsServiceRegistrationStep<CoreServicesRegistrationStep>
{
    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode", Justification = "BindConfiguration is intercepted by EnableConfigurationBindingGenerator")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "BindConfiguration is intercepted by EnableConfigurationBindingGenerator")]
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.TryAddScoped<IMutableUserContext, UserContext>();
        builder.Services.TryAddScoped<IUserContext>(sp => sp.GetRequiredService<IMutableUserContext>());
        builder.Services.TryAddScoped<IMutableInvocationContext, InvocationContext>();
        builder.Services.TryAddScoped<IInvocationContext>(sp => sp.GetRequiredService<IMutableInvocationContext>());

        builder.Services.TryAddScoped<ICommandSender, CommandSender>();
        builder.Services.TryAddScoped<INotificationPublisher, NotificationPublisher>();
        builder.Services.TryAddScoped<IRequestSender, RequestSender>();
        builder.Services.TryAddScoped<IEventPublisher, InMemoryEventPublisher>();

        builder.Services.TryAddScoped<ICommandRider, InMemoryCommandRider>();
        builder.Services.TryAddScoped<INotificationRider, InMemoryNotificationRider>();
        builder.Services.TryAddScoped<IRequestRider, InMemoryRequestRider>();
        builder.Services.TryAddSingleton<IMessageSerializer, JsonMessageSerializer>();

        // Handler resolvers: index handlers by message type + endpoint name for efficient lookup
        builder.Services.TryAddSingleton<ICommandHandlerResolver, CommandHandlerResolver>();
        builder.Services.TryAddSingleton<IRequestHandlerResolver, RequestHandlerResolver>();

        builder.Services.AddOptions<OutboxOptions>()
            .BindConfiguration("NOF:Outbox")
            .ValidateOnStart();

        // Handler inbound pipeline: executor
        builder.Services.TryAddScoped<IInboundPipelineExecutor, InboundPipelineExecutor>();

        // Outbound pipeline: executor
        builder.Services.TryAddScoped<IOutboundPipelineExecutor, OutboundPipelineExecutor>();

        // State machine registry
        builder.Services.TryAddSingleton<IStateMachineRegistry, StateMachineRegistry>();

        return ValueTask.CompletedTask;
    }
}
