using NOF.Contract;

namespace NOF.Infrastructure;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class LocalRpcClientAttribute<TRpcClient> : Attribute
    where TRpcClient : IRpcClient;
