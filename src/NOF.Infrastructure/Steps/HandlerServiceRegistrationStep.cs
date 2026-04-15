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
        var infos = builder.Services.GetOrAddSingleton<HandlerInfos>();
        var eventInfos = builder.Services.GetOrAddSingleton<EventHandlerInfos>();
        foreach (var registration in HandlerRegistry.GetRegistrations())
        {
            infos.Add(registration);
        }

        foreach (var info in infos.Commands)
        {
            TypeRegistry.Register(info.CommandType);
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        foreach (var info in eventInfos.Events)
        {
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        foreach (var info in infos.Notifications)
        {
            TypeRegistry.Register(info.NotificationType);
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        return ValueTask.CompletedTask;
    }
}
