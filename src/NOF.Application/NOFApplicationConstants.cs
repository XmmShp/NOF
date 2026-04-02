using System.Diagnostics;

namespace NOF.Application;

/// <summary>
/// Central constants for the NOF Application layer.
/// </summary>
public static class NOFApplicationConstants
{
    /// <summary>
    /// State machine tracing constants.
    /// </summary>
    public static class StateMachine
    {
        /// <summary>
        /// The ActivitySource name.
        /// </summary>
        public const string ActivitySourceName = "NOF.StateMachine";

        /// <summary>
        /// The ActivitySource instance.
        /// </summary>
        public static readonly ActivitySource Source = new(ActivitySourceName);

        /// <summary>
        /// Activity tag names.
        /// </summary>
        public static class Tags
        {
            public const string CorrelationId = "state_machine.correlation_id";
            public const string DefinitionType = "state_machine.definition_type";
            public const string FromState = "state_machine.from_state";
            public const string ToState = "state_machine.to_state";
            public const string NotificationType = "state_machine.notification_type";
        }

        /// <summary>
        /// Activity names.
        /// </summary>
        public static class ActivityNames
        {
            public const string StateTransition = "StateTransition";
        }
    }
}
