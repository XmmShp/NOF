namespace NOF;

public interface IStateMachineRegistry
{
    IReadOnlySet<StateMachineBlueprint> GetBlueprints<TNotification>();
}

public sealed class StateMachineRegistry : IStateMachineRegistry
{
    private readonly Dictionary<Type, HashSet<StateMachineBlueprint>> _blueprintsMap = [];

    public StateMachineRegistry(IEnumerable<StateMachineBlueprint> blueprints)
    {
        foreach (var blueprint in blueprints)
        {
            foreach (var notificationType in blueprint.ObservedNotificationTypes)
            {
                if (!_blueprintsMap.TryGetValue(notificationType, out var bpSet))
                {
                    bpSet = [];
                    _blueprintsMap.Add(notificationType, bpSet);
                }

                bpSet.Add(blueprint);
            }
        }
    }

    private static readonly IReadOnlySet<StateMachineBlueprint> EmptySet = new HashSet<StateMachineBlueprint>().AsReadOnly();
    public IReadOnlySet<StateMachineBlueprint> GetBlueprints<TNotification>()
    {
        return _blueprintsMap.TryGetValue(typeof(TNotification), out var bpSet)
            ? bpSet.AsReadOnly()
            : EmptySet;
    }
}