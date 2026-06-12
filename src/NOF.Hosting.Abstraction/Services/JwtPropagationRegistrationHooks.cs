namespace NOF.Hosting;

internal static class JwtPropagationRegistrationHooks
{
    private static readonly Lock SyncRoot = new();
    private static Action<INOFAppBuilder>? _onJwtPropagationAdded;

    public static void Register(Action<INOFAppBuilder> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (SyncRoot)
        {
            _onJwtPropagationAdded -= handler;
            _onJwtPropagationAdded += handler;
        }
    }

    public static void Invoke(INOFAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        Action<INOFAppBuilder>? handler;
        lock (SyncRoot)
        {
            handler = _onJwtPropagationAdded;
        }

        handler?.Invoke(builder);
    }
}
