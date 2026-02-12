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

        builder.Services.AddSingleton<IEndpointNameProvider>(new EndpointNameProvider());

        return ValueTask.CompletedTask;
    }
}
