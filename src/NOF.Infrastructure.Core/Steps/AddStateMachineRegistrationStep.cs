using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using System.Reflection;

namespace NOF.Infrastructure.Core;

public class AddStateMachineRegistrationStep : IDependentServiceRegistrationStep
{
    public ValueTask ExecuteAsync(INOFAppBuilder builder)
    {
        var assemblies = builder.Assemblies;
        var types = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false });

        var blueprints = new List<StateMachineBlueprint>();
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
                if (builderObj is not IStateMachineBuilderInternal typedBuilder)
                {
                    throw new InvalidOperationException(
                        $"Failed to cast builder instance of type '{builderType.FullName}' " +
                        $"to interface 'IStateMachineBuilderInternal'. " +
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
                var blueprint = typedBuilder.Build();
                ArgumentNullException.ThrowIfNull(blueprint);

                blueprint.DefinitionType = type;
                foreach (var notificationType in blueprint.ObservedNotificationTypes)
                {
                    var handlerType = typeof(StateMachineNotificationHandler<,>).MakeGenericType(type, notificationType);
#pragma warning disable CS8620 // https://github.com/dotnet/roslyn/issues/80024
                    builder.Services.AddHandlerInfo(new HandlerInfo(HandlerKind.Notification, handlerType, notificationType, null));
#pragma warning restore CS8620
                }
                blueprints.Add(blueprint);
            }
        }

        var registry = new StateMachineRegistry(blueprints);
        builder.Services.AddSingleton<IStateMachineRegistry>(registry);
        return ValueTask.CompletedTask;
    }
}
