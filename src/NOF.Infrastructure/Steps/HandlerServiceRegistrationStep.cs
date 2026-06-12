using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Abstraction;
using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Registers keyed transient services for all handler registries collected during base registration.
/// Runs after framework-host base registrations so handler registries are finalized.
/// </summary>
public class HandlerServiceRegistrationStep : IServiceRegistrationStep
{
    public TopologyComparison Compare(IServiceRegistrationStep other)
        => other is OpenTelemetryRegistrationStep ? TopologyComparison.After : TopologyComparison.DoesNotMatter;

    public ValueTask ExecuteAsync(IHostApplicationBuilder builder)
    {
        var typeResolver = builder.Services.GetOrAddSingleton<TypeResolver>();
        var commandHandlerRegistry = builder.Services.GetOrAddSingleton<CommandHandlerRegistry>();
        var eventHandlerRegistry = builder.Services.GetOrAddSingleton<EventHandlerRegistry>();
        var notificationHandlerRegistry = builder.Services.GetOrAddSingleton<NotificationHandlerRegistry>();

        foreach (var info in commandHandlerRegistry.Freeze())
        {
            typeResolver.Register(info.CommandType);
            typeResolver.Register(info.HandlerType);
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        foreach (var info in eventHandlerRegistry.Freeze())
        {
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        foreach (var info in notificationHandlerRegistry.Freeze())
        {
            typeResolver.Register(info.NotificationType);
            typeResolver.Register(info.HandlerType);
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        return ValueTask.CompletedTask;
    }
}
