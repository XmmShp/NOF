using System.ComponentModel;

namespace NOF;

[EditorBrowsable(EditorBrowsableState.Never)]
public class StatefulStateMachineContext
{
    public int State { get; set; }
    public required IStateMachineContext Context { get; init; }
}
