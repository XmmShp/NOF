namespace NOF.Contract;

/// <summary>
/// Marker interface for source-generated RPC clients.
/// </summary>
public interface IRpcClient;

/// <summary>
/// Associates an RPC client contract with the RPC service contract it invokes.
/// </summary>
/// <typeparam name="TRpcService">The RPC service contract.</typeparam>
public interface IRpcClient<TRpcService> : IRpcClient
    where TRpcService : IRpcService;
