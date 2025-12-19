using Microsoft.Extensions.DependencyInjection;
using NOF.Application.Internals;
using System.Reflection;

namespace NOF;

public class StateMachineConfig : IDependentServiceConfig
{
    public ValueTask ExecuteAsync(INOFAppBuilder builder)
    {
        var assemblies = builder.Assemblies;
        var types = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false });

        var startupRules = new List<IStateMachineOperation>();
        var transferRules = new List<IStateMachineOperation>();
        foreach (var type in types)
        {
            if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
            {
                continue;
            }
            var ifaces = type.GetInterfaces();
            foreach (var iface in ifaces)
            {
                if (!iface.IsGenericType)
                {
                    continue;
                }

                var def = iface.GetGenericTypeDefinition();
                if (def != typeof(IStateMachineDefinition<,>))
                {
                    continue;
                }

                var genericArguments = iface.GenericTypeArguments;
                var stateType = genericArguments[0];
                var contextType = genericArguments[1];
                var builderType = typeof(StateMachineBuilder<,>).MakeGenericType(stateType, contextType);
                var builderObj = Activator.CreateInstance(builderType);
                if (builderObj is not IStateMachineBuilder typedBuilder)
                {
                    throw new InvalidOperationException(
                        $"Failed to cast builder instance of type '{builderType.FullName}' " +
                        $"to interface 'IStateMachineBuilder'. " +
                        $"Ensure that '{builderType.FullName}' implements the expected state machine builder interface.");
                }

                var defObj = Activator.CreateInstance(type);
                if (defObj is not IStateMachineDefinition)
                {
                    throw new InvalidOperationException(
                        $"The type '{type.FullName}' was identified as implementing IStateMachineDefinition<,>, " +
                        $"but its instance could not be cast to 'IStateMachineDefinition'. " +
                        $"This may indicate a version mismatch, incorrect assembly loading, or a broken inheritance chain.");
                }

                var method = type.GetMethod(nameof(IStateMachineDefinition<,>.Build), BindingFlags.Instance | BindingFlags.Public);
                ArgumentNullException.ThrowIfNull(method);
                method.Invoke(defObj, [typedBuilder]);
                var (startup, transfer) = typedBuilder.Build();
                foreach (var operation in startup)
                {
                    var handlerType = typeof(StateMachineNotificationHandler<,>).MakeGenericType(type, operation.NotificationType);
                    builder.ExtraHandlerInfos.Add(new HandlerInfo(HandlerKind.Notification, handlerType, operation.NotificationType, null));
                    startupRules.Add(operation);
                }
                foreach (var operation in transfer)
                {
                    var handlerType = typeof(StateMachineNotificationHandler<,>).MakeGenericType(type, operation.NotificationType);
                    builder.ExtraHandlerInfos.Add(new HandlerInfo(HandlerKind.Notification, handlerType, operation.NotificationType, null));
                    transferRules.Add(operation);
                }
            }
        }

        var registry = new StateMachineRegistry(startupRules, transferRules);
        builder.Services.AddSingleton<IStateMachineRegistry>(registry);
        return ValueTask.CompletedTask;
    }
}
