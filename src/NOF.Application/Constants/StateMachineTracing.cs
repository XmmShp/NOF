using System.Diagnostics;

namespace NOF;

// StateMachineTracing.cs
public static class StateMachineTracing
{
    public const string StateMachineActivitySourceName = "NOF.StateMachines";
    public static readonly ActivitySource Source = new(StateMachineActivitySourceName);
}
