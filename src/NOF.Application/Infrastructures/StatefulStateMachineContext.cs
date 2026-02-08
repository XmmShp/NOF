using System.ComponentModel;

namespace NOF;

/// <summary>
/// Holds the current state and context of a state machine instance. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class StatefulStateMachineContext
{
    /// <summary>Gets or sets the current state value.</summary>
    public int State { get; set; }
    /// <summary>Gets the state machine context data.</summary>
    public required IStateMachineContext Context { get; init; }
}
