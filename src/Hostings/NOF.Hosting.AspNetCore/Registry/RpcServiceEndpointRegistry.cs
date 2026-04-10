using Microsoft.AspNetCore.Routing;

namespace NOF.Hosting.AspNetCore;

/// <summary>
/// Stores generated endpoint registration delegates for RPC services.
/// Entries are populated by assembly initializers generated at compile time.
/// </summary>
public static class RpcServiceEndpointRegistry
{
    private static readonly Lock _gate = new();
    private static readonly List<Action<IEndpointRouteBuilder>> _registrations = [];

    /// <summary>
    /// Registers an endpoint mapping delegate.
    /// </summary>
    public static void Register(Action<IEndpointRouteBuilder> registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        lock (_gate)
        {
            _registrations.Add(registration);
        }
    }

    /// <summary>
    /// Applies all registered endpoint mapping delegates to the target route builder.
    /// </summary>
    public static void MapAll(IEndpointRouteBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        Action<IEndpointRouteBuilder>[] registrations;
        lock (_gate)
        {
            registrations = [.. _registrations];
        }

        foreach (var registration in registrations)
        {
            registration(builder);
        }
    }
}
