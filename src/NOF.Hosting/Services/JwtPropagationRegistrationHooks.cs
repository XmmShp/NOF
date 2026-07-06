using Microsoft.Extensions.Hosting;

namespace NOF.Hosting;

internal static class JwtPropagationRegistrationHooks
{
    private static readonly Lock SyncRoot = new();
    private static Action<IHostApplicationBuilder>? _onJwtPropagationAdded;

    public static void Register(Action<IHostApplicationBuilder> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (SyncRoot)
        {
            _onJwtPropagationAdded -= handler;
            _onJwtPropagationAdded += handler;
        }
    }

    public static void Invoke(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        Action<IHostApplicationBuilder>? handler;
        lock (SyncRoot)
        {
            handler = _onJwtPropagationAdded;
        }

        handler?.Invoke(builder);
    }
}
