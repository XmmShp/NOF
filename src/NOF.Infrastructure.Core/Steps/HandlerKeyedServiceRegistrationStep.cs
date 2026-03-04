using Microsoft.Extensions.DependencyInjection;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.Core;

/// <summary>
/// Registers keyed scoped services for all handler infos collected during base registration.
/// Runs after all <see cref="IBaseSettingsServiceRegistrationStep"/>s so that handler infos
/// and endpoint name overrides are finalized.
/// </summary>
public class HandlerKeyedServiceRegistrationStep : IDependentServiceRegistrationStep<HandlerKeyedServiceRegistrationStep>
{
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        var commandInfos = builder.Services.GetOrAddSingleton<CommandHandlerInfos>();
        foreach (var info in commandInfos)
        {
            var ep = commandInfos.GetEndpointName(info.HandlerType);
            builder.Services.AddKeyedScoped(info.HandlerType, CommandHandlerKey.Of(info.CommandType, ep));
        }

        var eventInfos = builder.Services.GetOrAddSingleton<EventHandlerInfos>();
        foreach (var info in eventInfos)
        {
            var key = EventHandlerKey.Of(info.EventType);
            builder.Services.AddKeyedScoped(info.HandlerType, key);
            builder.Services.AddKeyedScoped<Application.IEventHandler>(key, (sp, k) => (Application.IEventHandler)sp.GetRequiredKeyedService(info.HandlerType, k));
        }

        var notificationInfos = builder.Services.GetOrAddSingleton<NotificationHandlerInfos>();
        foreach (var info in notificationInfos)
        {
            var key = NotificationHandlerKey.Of(info.NotificationType);
            builder.Services.AddKeyedScoped(info.HandlerType, key);
            builder.Services.AddKeyedScoped<Application.INotificationHandler>(key, (sp, k) => (Application.INotificationHandler)sp.GetRequiredKeyedService(info.HandlerType, k));
        }

        var reqInfos = builder.Services.GetOrAddSingleton<RequestWithoutResponseHandlerInfos>();
        foreach (var info in reqInfos)
        {
            var ep = reqInfos.GetEndpointName(info.HandlerType);
            builder.Services.AddKeyedScoped(info.HandlerType, RequestHandlerKey.Of(info.RequestType, ep));
        }

        var rwrInfos = builder.Services.GetOrAddSingleton<RequestWithResponseHandlerInfos>();
        foreach (var info in rwrInfos)
        {
            var ep = rwrInfos.GetEndpointName(info.HandlerType);
            builder.Services.AddKeyedScoped(info.HandlerType, RequestWithResponseHandlerKey.Of(info.RequestType, ep));
        }

        return ValueTask.CompletedTask;
    }
}
