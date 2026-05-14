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
        var registry = builder.GetOrAddRegistry();
        var commandInfos = builder.Services.GetOrAddSingleton(() => new CommandHandlerInfos(registry));
        var notificationInfos = builder.Services.GetOrAddSingleton(() => new NotificationHandlerInfos(registry));
        var eventInfos = builder.Services.GetOrAddSingleton(() => new EventHandlerInfos(registry));

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
