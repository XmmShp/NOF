using NOF.Contract;

namespace NOF.Infrastructure;

/// <summary>
/// Marker interface for local/in-process RPC client implementations targeting a generated contract client.
/// </summary>
public interface ILocalRpcClient<TRpcClient> where TRpcClient : IRpcClient;
