using System.Diagnostics;

namespace NOF;

// StateMachineTracing.cs
public static class StateMachineTracing
{
    public const string StateMachineActivitySourceName = "NOF.StateMachines";
    public static readonly ActivitySource Source = new(StateMachineActivitySourceName);

    /// <summary>
    /// Activity 标签名称
    /// </summary>
    public static class Tags
    {
        public const string CorrelationId = "nof.state_machine.correlation_id";
        public const string Type = "nof.state_machine.type";
        public const string StateFrom = "nof.state_machine.state_from";
        public const string StateTo = "nof.state_machine.state_to";
        public const string HandlerName = "nof.state_machine.handler_name";
        public const string TraceId = "nof.state_machine.trace_id";
        public const string SpanId = "nof.state_machine.span_id";
    }

    /// <summary>
    /// Activity 名称
    /// </summary>
    public static class ActivityNames
    {
        public const string Process = "nof.state_machine.process";
        public const string StateTransition = "nof.state_machine.state_transition";
        public const string Handler = "StateMachineHandler";
    }

    /// <summary>
    /// Baggage 键名
    /// </summary>
    public static class Baggage
    {
        public const string CorrelationId = "nof_sm_correlation_id";
        public const string Type = "nof_sm_type";
    }
}
