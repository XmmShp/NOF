using NOF.Contract;

namespace NOF.Hosting;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class HttpRpcClientAttribute<TRpcClient> : Attribute
    where TRpcClient : IRpcClient;
