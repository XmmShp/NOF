using System.Collections.Concurrent;
using NOF.Application;

namespace NOF.Infrastructure.Core;

internal sealed class StateMachineRegistry : IStateMachineRegistry
{
    private readonly ConcurrentDictionary<Type, Lazy<StateMachineBlueprint>> _blueprintsByDefinition = [];

    public StateMachineBlueprint? GetBlueprint(Type definitionType, Type notificationType, Func<StateMachineBlueprint> blueprintFactory)
    {
        var lazy = _blueprintsByDefinition.GetOrAdd(definitionType,
            _ => new Lazy<StateMachineBlueprint>(blueprintFactory));

        var blueprint = lazy.Value;
        return blueprint.ObservedTypes.Contains(notificationType) ? blueprint : null;
    }
}
