using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Registers keyed transient services for all handler registries collected during base registration.
/// Runs after all <see cref="IBaseSettingsServiceRegistrationStep"/>s so that handler registries are finalized.
/// </summary>
public class HandlerServiceRegistrationStep : IDependentServiceRegistrationStep
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        var registry = builder.GetOrAddRegistry();

        foreach (var info in registry.CommandHandlerRegistry.Freeze())
        {
            TypeRegistry.Register(info.CommandType);
            TypeRegistry.Register(info.HandlerType);
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        foreach (var info in registry.EventHandlerRegistry.Freeze())
        {
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        foreach (var info in registry.NotificationHandlerRegistry.Freeze())
        {
            TypeRegistry.Register(info.NotificationType);
            TypeRegistry.Register(info.HandlerType);
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        return ValueTask.CompletedTask;
    }
}
