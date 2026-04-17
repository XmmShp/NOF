using NOF.Hosting.AspNetCore;
using System.Collections.Concurrent;

namespace NOF.Abstraction;

public static class NOFHostingAspNetCoreRegistryExtensions
{
    private static readonly ConcurrentBag<RpcHttpEndpointHandlerRegistration> _rpcHttpEndpointHandlerRegistrations = [];

    extension(Registry)
    {
        public static ConcurrentBag<RpcHttpEndpointHandlerRegistration> RpcHttpEndpointHandlerRegistrations
            => _rpcHttpEndpointHandlerRegistrations;
    }
}
