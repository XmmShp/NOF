namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public enum OAuthClientType
{
    Confidential = 0,
    Public = 1
}

public sealed record OAuthClientClaim(string Type, string Value);

public sealed record OAuthClientDescriptor
{
    public required string ClientId { get; init; }

    public required string DisplayName { get; init; }

    public required IReadOnlyList<string> AllowedScopes { get; init; }

    public required IReadOnlyList<string> RedirectUris { get; init; }

    public required IReadOnlyList<OAuthClientClaim> AccessTokenClaims { get; init; }

    public required OAuthClientType ClientType { get; init; }

    public required bool IsEnabled { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}

public sealed record OAuthClientSecretDescriptor
{
    public required OAuthClientDescriptor Client { get; init; }

    public string? ClientSecret { get; init; }
}

public sealed record CreateOAuthClientRequest
{
    public string ClientId { get; init; } = string.Empty;

    public string? ClientSecret { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedScopes { get; init; } = [];

    public IReadOnlyList<string> RedirectUris { get; init; } = [];

    public IReadOnlyList<OAuthClientClaim> AccessTokenClaims { get; init; } = [];

    public OAuthClientType ClientType { get; init; } = OAuthClientType.Confidential;

    public bool IsEnabled { get; init; } = true;
}

public sealed record UpdateOAuthClientRequest
{
    public string DisplayName { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedScopes { get; init; } = [];

    public IReadOnlyList<string> RedirectUris { get; init; } = [];

    public IReadOnlyList<OAuthClientClaim> AccessTokenClaims { get; init; } = [];

    public OAuthClientType ClientType { get; init; } = OAuthClientType.Confidential;

    public bool IsEnabled { get; init; } = true;
}
