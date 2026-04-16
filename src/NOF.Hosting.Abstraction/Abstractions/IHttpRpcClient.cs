using NOF.Contract;

namespace NOF.Hosting;

/// <summary>
/// Marker interface for HTTP RPC client implementations targeting a generated contract client.
/// </summary>
public interface IHttpRpcClient<TRpcClient> where TRpcClient : IRpcClient;
