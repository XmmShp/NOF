using MassTransit;
using NOF.Hosting;

namespace NOF.Infrastructure.MassTransit;

internal class MassTransitRegistrationStep : IDependentServiceRegistrationStep<MassTransitRegistrationStep>
{
    internal Action<IBusRegistrationConfigurator>? ConfigureBus { get; set; }
    /// <inheritdoc/>
    public ValueTask ExecuteAsync(IServiceRegistrationContext builder)
    {
        var infos = builder.Services.GetOrAddSingleton<HandlerInfos>();
        var localHandlers = builder.Services.GetOrAddSingleton<LocalHandlerRegistry>();

        var mediatorConsumers = new List<Type>();
        var busConsumers = new List<Type>();

        // Command: mediator + bus
        foreach (var info in infos.Commands)
        {
            var adapter = typeof(MassTransitCommandHandlerAdapter<,>)
                .MakeGenericType(info.HandlerType, info.CommandType);
            localHandlers.Register(info.CommandType, infos.GetEndpointName(info.HandlerType));
            mediatorConsumers.Add(adapter);
            busConsumers.Add(adapter);
        }

        // Notification: bus only
        foreach (var info in infos.Notifications)
        {
            var adapter = typeof(MassTransitNotificationHandlerAdapter<,>)
                .MakeGenericType(info.HandlerType, info.NotificationType);
            busConsumers.Add(adapter);
        }

        builder.Services.AddMediator(config =>
        {
            config.AddConsumers(mediatorConsumers.ToArray());
        });

        builder.Services.AddMassTransit(config =>
        {
            config.SetEndpointNameFormatter(new EndpointNameFormatter(infos));
            config.AddConsumers(busConsumers.ToArray());
            ConfigureBus?.Invoke(config);
        });

        return ValueTask.CompletedTask;
    }
}
