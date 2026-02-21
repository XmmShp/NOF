namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Ordered list of handler middleware types used by <see cref="IInboundPipelineExecutor"/>.
/// Each <see cref="IInboundMiddlewareStep"/> appends its middleware type during service registration.
/// The execution order of steps (topologically sorted) IS the pipeline order.
/// <para>
/// Registered as a singleton in DI. Strong type avoids collision with other <c>List&lt;Type&gt;</c> registrations.
/// </para>
/// </summary>
public sealed class InboundPipelineTypes : List<Type>;
