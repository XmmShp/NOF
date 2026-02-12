using Microsoft.Extensions.DependencyInjection;
using NOF.Application;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers core framework services including invocation context, command sender,
/// notification publisher, and endpoint name provider.
/// </summary>
public class CoreServicesRegistrationStep : IBaseSettingsServiceRegistrationStep
{
    public ValueTask ExecuteAsync(INOFAppBuilder builder)
    {
        builder.Services.AddScoped<IInvocationContextInternal, InvocationContext>();
        builder.Services.AddScoped<IInvocationContext>(sp => sp.GetRequiredService<IInvocationContextInternal>());

        builder.Services.AddScoped<ICommandSender, CommandSender>();
        builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();
        builder.Services.AddScoped<IRequestSender, RequestSender>();

        builder.Services.AddSingleton<IEndpointNameProvider>(new EndpointNameProvider());

        builder.Services.AddOptionsInConfiguration<AuthorizationOptions>("NOF:Authorization");
        builder.Services.AddOptionsInConfiguration<OutboxOptions>("NOF:Outbox");

        // Handler inbound pipeline: register ordered type list + executor
        builder.Services.AddSingleton(new InboundPipelineTypes());
        builder.Services.AddScoped<IInboundPipelineExecutor, InboundPipelineExecutor>();

        // Outbound pipeline: register ordered type list + executor
        builder.Services.AddSingleton(new OutboundPipelineTypes());
        builder.Services.AddScoped<IOutboundPipelineExecutor, OutboundPipelineExecutor>();

        return ValueTask.CompletedTask;
    }
}
