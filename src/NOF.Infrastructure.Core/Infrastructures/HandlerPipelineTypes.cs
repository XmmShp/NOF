namespace NOF.Infrastructure.Core;

/// <summary>
/// Ordered list of handler middleware types used by <see cref="HandlerExecutor"/>.
/// Each <see cref="IHandlerMiddlewareStep"/> appends its middleware type during service registration.
/// The execution order of steps (topologically sorted) IS the pipeline order.
/// <para>
/// Registered as a singleton in DI. Strong type avoids collision with other <c>List&lt;Type&gt;</c> registrations.
/// </para>
/// </summary>
public sealed class HandlerPipelineTypes : List<Type>;
