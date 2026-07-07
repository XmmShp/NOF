using NOF.Contract;
using System.Reflection;
using System.Security.Claims;

namespace NOF.Infrastructure;

public interface IInboundAuthorizationHandler
{
    ValueTask<InboundAuthorizationResult> AuthorizeAsync(
        InboundAuthorizationContext context,
        CancellationToken cancellationToken);
}

public enum InboundAuthorizationKind
{
    Request,
    Command,
    Notification
}

public sealed class InboundAuthorizationContext
{
    public required InboundAuthorizationKind Kind { get; init; }

    public required ClaimsPrincipal User { get; init; }

    public required Context ExecutionContext { get; init; }

    public required object Input { get; init; }

    public required Type HandlerType { get; init; }

    public required MethodInfo HandlerMethodInfo { get; init; }

    public Type? ServiceType { get; init; }

    public MethodInfo? ServiceMethodInfo { get; init; }

    public Type? MessageType { get; init; }
}

public abstract record InboundAuthorizationResult
{
    public sealed record Allowed : InboundAuthorizationResult;

    public sealed record Denied(
        IResult Result,
        int StatusCode,
        IReadOnlyList<string> ChallengePermissions) : InboundAuthorizationResult;

    public static Allowed Success { get; } = new();
}
