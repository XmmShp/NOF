namespace NOF.Hosting;

internal static class RpcServerRegistrationHooks
{
    private static readonly Lock SyncRoot = new();
    private static Action<INOFAppBuilder, Type>? _onRpcServerAdded;

    public static void Register(Action<INOFAppBuilder, Type> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (SyncRoot)
        {
            _onRpcServerAdded -= handler;
            _onRpcServerAdded += handler;
        }
    }

    public static void Invoke(INOFAppBuilder builder, Type rpcServerType)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(rpcServerType);

        Action<INOFAppBuilder, Type>? handler;
        lock (SyncRoot)
        {
            handler = _onRpcServerAdded;
        }

        handler?.Invoke(builder, rpcServerType);
    }
}
