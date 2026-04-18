using NOF.Contract;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRpcServerServiceType
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
    static abstract Type ServiceType { get; }
}

public interface IRpcServer : IRpcServerServiceType
{
    static abstract IReadOnlyDictionary<string, RpcHandlerMapping> HandlerMappings { get; }
}

/// <summary>
/// One RPC operation mapping entry.
/// </summary>
public sealed record RpcHandlerMapping(
    Type HandlerType,
    Type RequestType,
    Type ReturnType);

/// <summary>
/// Base type for source-generated RPC server containers.
/// </summary>
public abstract class RpcServer
{

    /// <summary>
    /// Returns the mapping from operation name to split handler base type.
    /// </summary>
    protected abstract IReadOnlyDictionary<string, RpcHandlerMapping> GetHandlerMappings();

    /// <summary>
    /// Tries to resolve one operation mapping.
    /// </summary>
    public bool TryGetHandlerMapping(string operationName, [MaybeNullWhen(false)] out RpcHandlerMapping mapping)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return GetHandlerMappings().TryGetValue(operationName, out mapping);
    }
}

/// <summary>
/// Typed base class for an RPC server container.
/// </summary>
public abstract class RpcServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TRpcService> : RpcServer, IRpcServerServiceType
    where TRpcService : class, IRpcService
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
    public static Type ServiceType => typeof(TRpcService);
}
