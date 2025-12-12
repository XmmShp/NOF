using MassTransit;
using MassTransit.Internals;
using MassTransit.Util;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace NOF;

public interface IMassTransitConfiguring : IRegistrationConfigurator;

public record MassTransitConfiguring(IBusRegistrationConfigurator Configurator);

public class MassTransitConfigurator : IConfiguredServicesConfigurator, IDepsOn<IMassTransitConfiguring>
{
    private readonly IEnumerable<Assembly> _assemblies;

    public MassTransitConfigurator(IEnumerable<Assembly> assemblies)
    {
        _assemblies = assemblies;
    }

    public async ValueTask ExecuteAsync(INOFApp app)
    {
        var handlerEnumerable = await AssemblyTypeCache.FindTypes(
            _assemblies,
            TypeClassification.Concrete | TypeClassification.Closed,
            t => t.HasInterface<IRequestHandler>() || t.HasInterface<IEventHandler>() || t.HasInterface<ICommandHandler>() || t.HasInterface<INotificationHandler>()
            );
        var handlers = handlerEnumerable.ToArray();

        foreach (var handler in handlers)
        {
            app.Services.AddScoped(handler);
        }
        var internalConsumers = new List<Type>();
        var externalConsumers = new List<Type>();
        foreach (var (ic, ec) in handlers.Select(MakeConsumerTypes))
        {
            internalConsumers.AddRange(ic);
            externalConsumers.AddRange(ec);
        }

        app.Services.AddMediator(config =>
        {
            config.AddConsumers(internalConsumers.ToArray());
        });

        app.Services.AddMassTransit(config =>
        {
            config.SetEndpointNameFormatter(EndpointNameFormatter.Instance);
            config.AddConsumers(externalConsumers.ToArray());
            EventDispatcher.Publish(new MassTransitConfiguring(config));
        });
    }

    internal static (Type[], Type[]) MakeConsumerTypes(Type handlerType)
    {
        var interfaces = handlerType.GetInterfaces();

        var requestWithoutResponseInfos = new List<Type>(); // IRequestHandler<T> -> T
        var requestWithResponseInfos = new List<(Type, Type)>(); // IRequestHandler<T, TResponse> -> (T, TResponse)

        var asyncCommandInfos = new List<Type>(); // IAsyncCommandHandler<T> -> T
        var commandWithoutResponseInfos = new List<Type>(); // ICommandHandler<T> -> T
        var commandWithResponseInfos = new List<(Type, Type)>(); // ICommandHandler<T, TResponse> -> (T, TResponse)

        var eventInfos = new List<Type>(); // IEventHandler<T> -> T

        var notificationInfos = new List<Type>(); // INotificationHandler<T> -> T

        ScanHandlers();
        return GetConsumerTypes();

        void ScanHandlers()
        {
            foreach (var @interface in interfaces.Where(i => i.IsGenericType))
            {
                var definition = @interface.GetGenericTypeDefinition();

                if (definition == typeof(IRequestHandler<>))
                {
                    requestWithoutResponseInfos.Add(@interface.GenericTypeArguments[0]);
                }
                else if (definition == typeof(IRequestHandler<,>))
                {
                    var args = @interface.GenericTypeArguments;
                    requestWithResponseInfos.Add((args[0], args[1]));
                }
                else if (definition == typeof(IEventHandler<>))
                {
                    eventInfos.Add(@interface.GenericTypeArguments[0]);
                }
                else if (definition == typeof(IAsyncCommandHandler<>))
                {
                    asyncCommandInfos.Add(@interface.GenericTypeArguments[0]);
                }
                else if (definition == typeof(ICommandHandler<>))
                {
                    commandWithoutResponseInfos.Add(@interface.GenericTypeArguments[0]);
                }
                else if (definition == typeof(ICommandHandler<,>))
                {
                    var args = @interface.GenericTypeArguments;
                    commandWithResponseInfos.Add((args[0], args[1]));
                }
                else if (definition == typeof(INotificationHandler<>))
                {
                    notificationInfos.Add(@interface.GenericTypeArguments[0]);
                }
            }
        }

        (Type[], Type[]) GetConsumerTypes()
        {
            var internalTypes = new List<Type>();
            var externalTypes = new List<Type>();

            internalTypes.AddRange(requestWithoutResponseInfos.Select(requestType => typeof(NOFRequestConsumer<,>).MakeGenericType(handlerType, requestType)));
            internalTypes.AddRange(requestWithResponseInfos.Select(requestInfo => typeof(NOFRequestConsumer<,,>).MakeGenericType(handlerType, requestInfo.Item1, requestInfo.Item2)));
            internalTypes.AddRange(eventInfos.Select(eventType => typeof(NOFEventConsumer<,>).MakeGenericType(handlerType, eventType)));
            externalTypes.AddRange(asyncCommandInfos.Select(commandType => typeof(NOFAsyncCommandConsumer<,>).MakeGenericType(handlerType, commandType)));
            externalTypes.AddRange(commandWithoutResponseInfos.Select(commandType => typeof(NOFCommandConsumer<,>).MakeGenericType(handlerType, commandType)));
            externalTypes.AddRange(commandWithResponseInfos.Select(commandInfo => typeof(NOFCommandConsumer<,,>).MakeGenericType(handlerType, commandInfo.Item1, commandInfo.Item2)));
            externalTypes.AddRange(notificationInfos.Select(notificationType => typeof(NOFNotificationConsumer<,>).MakeGenericType(handlerType, notificationType)));
            return (internalTypes.ToArray(), externalTypes.ToArray());
        }
    }
}