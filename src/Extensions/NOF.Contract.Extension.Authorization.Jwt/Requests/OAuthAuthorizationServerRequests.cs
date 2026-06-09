using NOF.Contract;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

public sealed record OAuthServerRootRequest;

public sealed record OAuthServerMetadataRequest;

public sealed record OAuthJwksRequest;

public sealed record OAuthAuthorizeRequest
{
    public string ResponseType { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string? Nonce { get; set; }

    public string? CodeChallenge { get; set; }

    public string? CodeChallengeMethod { get; set; }
}

public sealed record OAuthTokenRequest
{
    public string GrantType { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public string CodeVerifier { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;
}

public sealed record OAuthUserInfoRequest
{
    [FromHeader("Authorization")]
    public BearerToken AccessToken { get; set; }
}

public readonly record struct BearerToken(string Value) : ITransportStringParsable<BearerToken>
{
    public static bool TryParse(string? value, IFormatProvider? provider, out BearerToken result)
    {
        var token = value?.Trim() ?? string.Empty;
        const string prefix = "Bearer ";
        if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            token = token[prefix.Length..].TrimStart();
        }

        result = new BearerToken(token);
        return !string.IsNullOrWhiteSpace(token);
    }

    public override string ToString() => Value;
}
