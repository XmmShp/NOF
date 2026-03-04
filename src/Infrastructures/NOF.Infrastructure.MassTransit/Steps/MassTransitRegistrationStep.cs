using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NOF.Infrastructure.Abstraction;

namespace NOF.Infrastructure.MassTransit;

internal class MassTransitRegistrationStep : IDependentServiceRegistrationStep<MassTransitRegistrationStep>
{
    internal Action<IBusRegistrationConfigurator>? ConfigureBus { get; set; }
    /// <inheritdoc/>
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        var commandInfos = builder.Services.GetOrAddSingleton<CommandHandlerInfos>();
        var notificationInfos = builder.Services.GetOrAddSingleton<NotificationHandlerInfos>();
        var requestWithoutResponseInfos = builder.Services.GetOrAddSingleton<RequestWithoutResponseHandlerInfos>();
        var requestWithResponseInfos = builder.Services.GetOrAddSingleton<RequestWithResponseHandlerInfos>();

        var localHandlers = builder.Services.GetOrAddSingleton<LocalHandlerRegistry>();

        var mediatorConsumers = new List<Type>();
        var busConsumers = new List<Type>();

        // Command: mediator + bus
        foreach (var info in commandInfos)
        {
            var adapter = typeof(MassTransitCommandHandlerAdapter<,>)
                .MakeGenericType(info.HandlerType, info.CommandType);
            localHandlers.Register(info.CommandType, commandInfos.GetEndpointName(info.HandlerType));
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
            localHandlers.Register(info.RequestType, requestWithoutResponseInfos.GetEndpointName(info.HandlerType));
            mediatorConsumers.Add(adapter);
            busConsumers.Add(adapter);
        }

        // RequestWithResponse: mediator + bus
        foreach (var info in requestWithResponseInfos)
        {
            var adapter = typeof(MassTransitRequestHandlerAdapter<,,>)
                .MakeGenericType(info.HandlerType, info.RequestType, info.ResponseType);
            localHandlers.Register(info.RequestType, requestWithResponseInfos.GetEndpointName(info.HandlerType));
            mediatorConsumers.Add(adapter);
            busConsumers.Add(adapter);
        }

        builder.Services.AddMediator(config =>
        {
            config.AddConsumers(mediatorConsumers.ToArray());
        });

        builder.Services.AddMassTransit(config =>
        {
            config.SetEndpointNameFormatter(new EndpointNameFormatter(commandInfos, requestWithoutResponseInfos, requestWithResponseInfos));
            config.AddConsumers(busConsumers.ToArray());
            ConfigureBus?.Invoke(config);
        });

        return ValueTask.CompletedTask;
    }
}
