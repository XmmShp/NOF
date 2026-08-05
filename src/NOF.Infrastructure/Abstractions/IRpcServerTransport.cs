using Microsoft.Extensions.Hosting;
using NOF.Application;
using NOF.Hosting;

namespace NOF.Infrastructure;

/// <summary>
/// Defines a transport-specific RPC server mapping extension.
/// </summary>
public interface IRpcServerTransport : ITopologizable<IRpcServerTransport>
{
    /// <summary>
    /// Maps or initializes one RPC server registration for this transport.
    /// </summary>
    Task MapAsync(IHost host, RpcServerRegistration registration);
}
