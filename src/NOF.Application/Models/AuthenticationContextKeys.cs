using NOF.Contract;

namespace NOF.Application;

/// <summary>
/// Well-known <see cref="Context"/> item keys used to direct outbound authentication behavior.
/// </summary>
public static class AuthenticationContextKeys
{
    public static object ServiceTokenHeader { get; } = new();

    public static object TokenExchangeHeaders { get; } = new();
}
