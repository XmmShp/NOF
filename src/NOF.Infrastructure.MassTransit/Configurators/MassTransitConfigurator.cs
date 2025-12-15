using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Reflection;

namespace NOF;

public record MassTransitConfiguring(IBusRegistrationConfigurator Configurator);

internal class MassTransitConfig : IDependentServiceConfig
{
    /// <inheritdoc/>
    public ValueTask ExecuteAsync(INOFAppBuilder builder)
    {
        var handlerInfos = builder.HandlerInfos;

        // Register all distinct handler types as scoped services
        var handlers = handlerInfos.Select(i => i.HandlerType).Distinct();
        foreach (var handler in handlers)
        {
            builder.Services.AddScoped(handler);
        }

        var internalConsumers = new List<Type>();  // For Mediator (internal processing, e.g., HTTP-triggered)
        var externalConsumers = new List<Type>();  // For MassTransit (message queue consumers)

        foreach (var handlerInfo in handlerInfos)
        {
            // Event handlers are always processed internally via Mediator and never exposed to MassTransit
            if (handlerInfo.Kind == HandlerKind.Event)
            {
                MediatorRequestHandleNode.SupportedRequestTypes.Add(handlerInfo.MessageType);
                internalConsumers.Add(CreateHandlerAdapter(handlerInfo));
                continue; // Skip further processing for events
            }

            // Determine if this request type is exposed via HTTP
            var isExposedToHttp = handlerInfo.MessageType.GetCustomAttributes<ExposeToHttpEndpointAttribute>().Any();

            if (isExposedToHttp)
            {
                // HTTP-exposed requests are handled internally by Mediator
                MediatorRequestHandleNode.SupportedRequestTypes.Add(handlerInfo.MessageType);
                internalConsumers.Add(CreateHandlerAdapter(handlerInfo));

                // Check if an explicit endpoint name is defined (via attribute on message or handler)
                var hasEndpointName =
                    handlerInfo.MessageType.GetCustomAttribute<EndpointNameAttribute>() is not null ||
                    handlerInfo.HandlerType.GetCustomAttributes<EndpointNameAttribute>().Any();

                // If an explicit endpoint name is provided, also register as a MassTransit consumer
                // This allows the same handler to be invoked via message queues in addition to HTTP
                if (hasEndpointName)
                {
                    externalConsumers.Add(CreateHandlerAdapter(handlerInfo));
                }
            }
            else
            {
                // Non-HTTP handlers are only consumed via MassTransit (external messaging)
                externalConsumers.Add(CreateHandlerAdapter(handlerInfo));
            }
        }

        builder.Services.AddMediator(config =>
        {
            config.AddConsumers(internalConsumers.ToArray());
        });

        builder.Services.AddMassTransit(config =>
        {
            config.SetEndpointNameFormatter(EndpointNameFormatter.Instance);
            config.AddConsumers(externalConsumers.ToArray());
            builder.EventDispatcher.Publish(new MassTransitConfiguring(config));
        });

        return ValueTask.CompletedTask;
    }

    #region Helper Methods

    /// <summary>
    /// Creates the appropriate MassTransit request/command/notification handler adapter type
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