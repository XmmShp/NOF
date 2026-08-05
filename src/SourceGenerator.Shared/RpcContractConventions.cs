using System;

namespace NOF.SourceGeneration;

/// <summary>
/// Naming conventions shared by RPC generators. Every value is derived exclusively from
/// user-authored RPC service/server declarations; generated symbols are never inspected.
/// </summary>
internal static class RpcContractConventions
{
    public static string GetClientInterfaceName(string serviceInterfaceName)
        => serviceInterfaceName + "Client";

    public static string GetHttpClientName(string serviceInterfaceName)
        => "Http" + GetServiceBaseName(serviceInterfaceName) + "Client";

    public static string GetLocalClientName(string rpcServerName)
        => "Local" + rpcServerName + "Client";

    private static string GetServiceBaseName(string serviceInterfaceName)
        => serviceInterfaceName.StartsWith("I", StringComparison.Ordinal)
           && serviceInterfaceName.Length > 1
           && char.IsUpper(serviceInterfaceName[1])
            ? serviceInterfaceName.Substring(1)
            : serviceInterfaceName;
}
