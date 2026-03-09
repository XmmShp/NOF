using System.ComponentModel;

namespace NOF.Application;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IStateMachineRegistry
{
    StateMachineBlueprint? GetBlueprint(Type definitionType, Type notificationType, Func<StateMachineBlueprint> blueprintFactory);
}
