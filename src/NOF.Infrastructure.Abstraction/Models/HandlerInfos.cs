namespace NOF.Infrastructure.Abstraction;

/// <summary>
/// Strongly-typed set of <see cref="CommandHandlerInfo"/> metadata discovered at compile time
/// (via source generator) or added manually at runtime.
/// Registered as a singleton in DI.
/// </summary>
public sealed class CommandHandlerInfos : HashSet<CommandHandlerInfo>;

/// <summary>
/// Strongly-typed set of <see cref="EventHandlerInfo"/> metadata discovered at compile time
/// (via source generator) or added manually at runtime.
/// Registered as a singleton in DI.
/// </summary>
public sealed class EventHandlerInfos : HashSet<EventHandlerInfo>;

/// <summary>
/// Strongly-typed set of <see cref="NotificationHandlerInfo"/> metadata discovered at compile time
/// (via source generator) or added manually at runtime.
/// Registered as a singleton in DI.
/// </summary>
public sealed class NotificationHandlerInfos : HashSet<NotificationHandlerInfo>;

/// <summary>
/// Strongly-typed set of <see cref="RequestWithoutResponseHandlerInfo"/> metadata discovered at compile time
/// (via source generator) or added manually at runtime.
/// Registered as a singleton in DI.
/// </summary>
public sealed class RequestWithoutResponseHandlerInfos : HashSet<RequestWithoutResponseHandlerInfo>;

/// <summary>
/// Strongly-typed set of <see cref="RequestWithResponseHandlerInfo"/> metadata discovered at compile time
/// (via source generator) or added manually at runtime.
/// Registered as a singleton in DI.
/// </summary>
public sealed class RequestWithResponseHandlerInfos : HashSet<RequestWithResponseHandlerInfo>;
