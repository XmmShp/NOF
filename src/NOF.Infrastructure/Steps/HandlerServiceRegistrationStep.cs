using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Registers keyed transient services for all handler infos collected during base registration.
/// Runs after all <see cref="IBaseSettingsServiceRegistrationStep"/>s so that handler infos are finalized.
/// </summary>
public class HandlerServiceRegistrationStep : IDependentServiceRegistrationStep
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        var commandInfos = builder.Services.GetOrAddSingleton<CommandHandlerInfos>();
        var notificationInfos = builder.Services.GetOrAddSingleton<NotificationHandlerInfos>();
        var eventInfos = builder.Services.GetOrAddSingleton<EventHandlerInfos>();

        foreach (var info in commandInfos.Registrations)
        {
            TypeRegistry.Register(info.CommandType);
            TypeRegistry.Register(info.HandlerType);
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        foreach (var info in eventInfos.Events)
        {
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        foreach (var info in notificationInfos.Registrations)
        {
            TypeRegistry.Register(info.NotificationType);
            TypeRegistry.Register(info.HandlerType);
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        return ValueTask.CompletedTask;
    }
}
