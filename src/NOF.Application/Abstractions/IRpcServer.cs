namespace NOF.Application;

/// <summary>
/// Exposes metadata for a source-generated RPC server.
/// </summary>
public interface IRpcServer
{
    /// <summary>
    /// Gets the RPC contract type handled by this server.
    /// </summary>
    Type ServiceType { get; }

    /// <summary>
    /// Tries to resolve the split handler base type for one operation.
    /// </summary>
    bool TryGetHandlerType(string operationName, out Type handlerType);
}
