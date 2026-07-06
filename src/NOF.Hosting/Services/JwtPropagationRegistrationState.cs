using Microsoft.Extensions.Hosting;

namespace NOF.Hosting;

internal static class JwtPropagationRegistrationState
{
    private static readonly object _jwtPropagationEnabledKey = new();
    private static readonly object _jwtPropagationRegistrationsKey = new();

    public static void Register(IHostApplicationBuilder builder, Action<IHostApplicationBuilder> handler)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(handler);

        var registrations = GetOrAddRegistrations(builder);
        registrations.Remove(handler);
        registrations.Add(handler);

        if (IsJwtPropagationEnabled(builder))
        {
            handler(builder);
        }
    }

    public static void MarkJwtPropagationAdded(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Properties.TryAdd(_jwtPropagationEnabledKey, true))
        {
            return;
        }

        foreach (var registration in GetOrAddRegistrations(builder))
        {
            registration(builder);
        }
    }

    private static bool IsJwtPropagationEnabled(IHostApplicationBuilder builder)
        => builder.Properties.TryGetValue(_jwtPropagationEnabledKey, out var value)
            && value is true;

    private static List<Action<IHostApplicationBuilder>> GetOrAddRegistrations(IHostApplicationBuilder builder)
    {
        if (builder.Properties.TryGetValue(_jwtPropagationRegistrationsKey, out var existing)
            && existing is List<Action<IHostApplicationBuilder>> registrations)
        {
            return registrations;
        }

        registrations = [];
        builder.Properties[_jwtPropagationRegistrationsKey] = registrations;
        return registrations;
    }
}
