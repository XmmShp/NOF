namespace NOF.Sample;

public sealed record CreateDemoOAuthClientRequest
{
    public string ClientIdPrefix { get; init; } = "sample-client";
}

public sealed record CreateDemoOAuthClientResponse
{
    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }

    public required string[] AllowedScopes { get; init; }
}

public sealed record ConsumeDemoAccessTokenRequest;

public sealed record ConsumeDemoAccessTokenResponse
{
    public bool IsAuthenticated { get; init; }

    public string? Subject { get; init; }

    public string? TenantId { get; init; }

    public string? ProxyServiceName { get; init; }

    public required string[] Permissions { get; init; }

    public required string[] Scopes { get; init; }
}

public sealed record GetDemoClientTokenRequest
{
    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }
}

public sealed record GetDemoUserTokenRequest;

public sealed record ExchangeDemoTokenRequest
{
    public required string ClientAccessToken { get; init; }

    public required string UserAccessToken { get; init; }

    public string RequestedScope { get; init; } = "sample.read";
}

public sealed record DemoTokenResponse
{
    public required string AccessToken { get; init; }

    public string TokenType { get; init; } = "Bearer";

    public long? ExpiresIn { get; init; }

    public string? Scope { get; init; }

    public string? IdToken { get; init; }
}

public sealed record CallDemoDownstreamRequest
{
    public required string AccessToken { get; init; }
}
