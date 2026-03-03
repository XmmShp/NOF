using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Infrastructure.Abstraction;
using NOF.Infrastructure.Core;

namespace NOF.Infrastructure.MassTransit;

public record MassTransitConfiguring(IBusRegistrationConfigurator Configurator);

internal class MassTransitRegistrationStep : IDependentServiceRegistrationStep<MassTransitRegistrationStep>
{
    /// <inheritdoc/>
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        var commandInfos = builder.Services.GetOrAddSingleton<CommandHandlerInfos>();
        var notificationInfos = builder.Services.GetOrAddSingleton<NotificationHandlerInfos>();
        var requestWithoutResponseInfos = builder.Services.GetOrAddSingleton<RequestWithoutResponseHandlerInfos>();
        var requestWithResponseInfos = builder.Services.GetOrAddSingleton<RequestWithResponseHandlerInfos>();

        var localHandlers = builder.Services.GetOrAddSingleton<LocalHandlerRegistry>();
        var endpointNameOptions = builder.Services.GetOrAddSingleton<EndpointNameOptions>();
        var nameProvider = new ManualEndpointNameProvider(Options.Create(endpointNameOptions));

        // Collect all non-event handler types for scoped registration
        var handlerTypes = new HashSet<Type>();
        foreach (var info in commandInfos)
        {
            handlerTypes.Add(info.HandlerType);
        }

        foreach (var info in notificationInfos)
        {
            handlerTypes.Add(info.HandlerType);
        }

        foreach (var info in requestWithoutResponseInfos)
        {
            handlerTypes.Add(info.HandlerType);
        }

        foreach (var info in requestWithResponseInfos)
        {
            handlerTypes.Add(info.HandlerType);
        }

        foreach (var handler in handlerTypes)
        {
            builder.Services.AddScoped(handler);
        }

        var mediatorConsumers = new List<Type>();
        var busConsumers = new List<Type>();

        // Command: mediator + bus
        foreach (var info in commandInfos)
        {
            var adapter = typeof(MassTransitCommandHandlerAdapter<,>)
                .MakeGenericType(info.HandlerType, info.CommandType);
            localHandlers.Register(info.CommandType, nameProvider.GetEndpointName(info.HandlerType));
            mediatorConsumers.Add(adapter);
            busConsumers.Add(adapter);
        }

        // Notification: bus only
        foreach (var info in notificationInfos)
        {
            var adapter = typeof(MassTransitNotificationHandlerAdapter<,>)
                .MakeGenericType(info.HandlerType, info.NotificationType);
            busConsumers.Add(adapter);
        }

        // RequestWithoutResponse: mediator + bus
        foreach (var info in requestWithoutResponseInfos)
        {
            var adapter = typeof(MassTransitRequestHandlerAdapter<,>)
                .MakeGenericType(info.HandlerType, info.RequestType);
            localHandlers.Register(info.RequestType, nameProvider.GetEndpointName(info.HandlerType));
            mediatorConsumers.Add(adapter);
            busConsumers.Add(adapter);
        }

        // RequestWithResponse: mediator + bus
        foreach (var info in requestWithResponseInfos)
        {
            var adapter = typeof(MassTransitRequestHandlerAdapter<,,>)
                .MakeGenericType(info.HandlerType, info.RequestType, info.ResponseType);
            localHandlers.Register(info.RequestType, nameProvider.GetEndpointName(info.HandlerType));
            mediatorConsumers.Add(adapter);
            busConsumers.Add(adapter);
        }

        builder.Services.AddMediator(config =>
        {
            config.AddConsumers(mediatorConsumers.ToArray());
        });

        builder.Services.AddMassTransit(config =>
        {
            config.SetEndpointNameFormatter(new EndpointNameFormatter(nameProvider));
            config.AddConsumers(busConsumers.ToArray());
            builder.StartupEventChannel.Publish(new MassTransitConfiguring(config));
        });

        return ValueTask.CompletedTask;
    }
}
