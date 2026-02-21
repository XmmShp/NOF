using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.MassTransit;

public record MassTransitConfiguring(IBusRegistrationConfigurator Configurator);

internal class MassTransitRegistrationStep : IDependentServiceRegistrationStep
{
    /// <inheritdoc/>
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        var handlerInfos = builder.HandlerInfos;
        var localHandlers = builder.Services.GetOrAddSingleton<LocalHandlerRegistry>();
        var nameProvider = builder.EndpointNameProvider!;

        // Register all distinct handler types as scoped services
        var handlers = handlerInfos.Select(i => i.HandlerType).Distinct();
        foreach (var handler in handlers)
        {
            builder.Services.AddScoped(handler);
        }

        var mediatorConsumers = new List<Type>();
        var busConsumers = new List<Type>();

        foreach (var handlerInfo in handlerInfos)
        {
            var adapter = CreateHandlerAdapter(handlerInfo);

            switch (handlerInfo.Kind)
            {
                // Event: always local mediator only
                case HandlerKind.Event:
                    localHandlers.Register(handlerInfo.MessageType, nameProvider.GetEndpointName(handlerInfo.HandlerType));
                    mediatorConsumers.Add(adapter);
                    break;

                // Notification: always bus publish only
                case HandlerKind.Notification:
                    busConsumers.Add(adapter);
                    break;

                // Command / Request: always register on BOTH mediator AND bus
                case HandlerKind.Command:
                case HandlerKind.RequestWithoutResponse:
                case HandlerKind.RequestWithResponse:
                    localHandlers.Register(handlerInfo.MessageType, nameProvider.GetEndpointName(handlerInfo.HandlerType));
                    mediatorConsumers.Add(adapter);
                    busConsumers.Add(adapter);
                    break;
            }
        }

        builder.Services.AddMediator(config =>
        {
            config.AddConsumers(mediatorConsumers.ToArray());
        });

        builder.Services.AddMassTransit(config =>
        {
            config.SetEndpointNameFormatter(new EndpointNameFormatter(builder.EndpointNameProvider!));
            config.AddConsumers(busConsumers.ToArray());
            builder.StartupEventChannel.Publish(new MassTransitConfiguring(config));
        });

        return ValueTask.CompletedTask;
    }

    #region Helper Methods

    /// <summary>
    /// Creates the appropriate MassTransit handler adapter type
    /// based on the handler kind and response type.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the handler kind is not supported.</exception>
    private static Type CreateHandlerAdapter(HandlerInfo info)
    {
        return info.Kind switch
        {
            HandlerKind.Event =>
                typeof(MassTransitEventHandlerAdapter<,>)
                    .MakeGenericType(info.HandlerType, info.MessageType),

            HandlerKind.Command =>
                typeof(MassTransitCommandHandlerAdapter<,>)
                    .MakeGenericType(info.HandlerType, info.MessageType),

            HandlerKind.RequestWithoutResponse =>
                typeof(MassTransitRequestHandlerAdapter<,>)
                    .MakeGenericType(info.HandlerType, info.MessageType),

            HandlerKind.RequestWithResponse when info.ResponseType is not null =>
                typeof(MassTransitRequestHandlerAdapter<,,>)
                    .MakeGenericType(info.HandlerType, info.MessageType, info.ResponseType),

            HandlerKind.Notification =>
                typeof(MassTransitNotificationHandlerAdapter<,>)
                    .MakeGenericType(info.HandlerType, info.MessageType),

            _ => throw new InvalidOperationException($"Unsupported handler kind: {info.Kind}")
        };
    }
    #endregion
}
