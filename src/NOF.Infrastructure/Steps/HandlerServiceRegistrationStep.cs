using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
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

    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        var registry = builder.Registry;
        var typeResolver = builder.Services.GetOrAddSingleton<TypeResolver>();

        foreach (var info in registry.CommandHandlerRegistry.Freeze())
        {
            typeResolver.Register(info.CommandType);
            typeResolver.Register(info.HandlerType);
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        foreach (var info in registry.EventHandlerRegistry.Freeze())
        {
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        foreach (var info in registry.NotificationHandlerRegistry.Freeze())
        {
            typeResolver.Register(info.NotificationType);
            typeResolver.Register(info.HandlerType);
            builder.Services.ReplaceOrAdd(ServiceDescriptor.Transient(info.HandlerType, info.HandlerType));
        }

        return ValueTask.CompletedTask;
    }
}
