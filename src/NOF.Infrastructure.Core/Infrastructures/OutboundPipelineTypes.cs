namespace NOF.Infrastructure.Core;

/// <summary>
/// Ordered list of outbound middleware types used by <see cref="OutboundPipelineExecutor"/>.
/// Each <see cref="IOutboundMiddlewareStep"/> appends its middleware type during service registration.
/// The execution order of steps (topologically sorted) IS the outbound pipeline order.
/// <para>
/// Registered as a singleton in DI. Strong type avoids collision with other list registrations.
/// </para>
/// </summary>
public sealed class OutboundPipelineTypes : List<Type>;
