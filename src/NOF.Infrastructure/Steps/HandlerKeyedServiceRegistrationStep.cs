using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Registers keyed scoped services for all handler infos collected during base registration.
/// Runs after all <see cref="IBaseSettingsServiceRegistrationStep"/>s so that handler infos
/// and endpoint name overrides are finalized.
/// </summary>
public class HandlerKeyedServiceRegistrationStep : IDependentServiceRegistrationStep<HandlerKeyedServiceRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        var infos = builder.Services.GetOrAddSingleton<HandlerInfos>();

        foreach (var info in infos.Commands)
        {
            TypeRegistry.Register(info.CommandType);
            var ep = infos.GetEndpointName(info.HandlerType);
            builder.Services.AddKeyedScoped(info.HandlerType, CommandHandlerKey.Of(info.CommandType, ep));
        }

        foreach (var info in infos.Events)
        {
            var key = EventHandlerKey.Of(info.EventType);
            builder.Services.AddKeyedScoped(info.HandlerType, key);
            builder.Services.AddKeyedScoped(key, (sp, k) => (IEventHandler)sp.GetRequiredKeyedService(info.HandlerType, k));
        }

        foreach (var info in infos.Notifications)
        {
            TypeRegistry.Register(info.NotificationType);
            var key = NotificationHandlerKey.Of(info.NotificationType);
            builder.Services.AddKeyedScoped(info.HandlerType, key);
            builder.Services.AddKeyedScoped(key, (sp, k) => (INotificationHandler)sp.GetRequiredKeyedService(info.HandlerType, k));
        }

        return ValueTask.CompletedTask;
    }
}
