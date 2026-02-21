namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Strongly-typed set of <see cref="HandlerInfo"/> metadata discovered at compile time
/// (via source generator) or added manually at runtime.
/// <para>
/// Registered as a singleton in DI. Strong type avoids collision with other <c>HashSet&lt;HandlerInfo&gt;</c> registrations.
/// </para>
/// </summary>
public sealed class HandlerInfos : HashSet<HandlerInfo>;
