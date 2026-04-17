using NOF.Contract;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

public interface IRpcServer
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
    static abstract Type ServiceType { get; }
}

/// <summary>
/// Base type for source-generated RPC server containers.
/// </summary>
public abstract class RpcServer
{
    /// <summary>
    /// Returns the mapping from operation name to split handler base type.
    /// </summary>
    protected abstract IReadOnlyDictionary<string, Type> GetHandlerMappings();

    /// <summary>
    /// Tries to resolve the split handler base type for one operation.
    /// </summary>
    public bool TryGetHandlerType(string operationName, out Type handlerType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return GetHandlerMappings().TryGetValue(operationName, out handlerType!);
    }

    /// <summary>
    /// Returns all known handler mappings.
    /// </summary>
    public IReadOnlyDictionary<string, Type> GetAllHandlerMappings()
        => new ReadOnlyDictionary<string, Type>(new Dictionary<string, Type>(GetHandlerMappings()));
}

/// <summary>
/// Typed base class for an RPC server container.
/// </summary>
public abstract class RpcServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TRpcService> : RpcServer, IRpcServer
    where TRpcService : class, IRpcService
{
    public static Type ServiceType => typeof(TRpcService);
}
