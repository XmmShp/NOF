using System.Text.Json.Serialization;

namespace NOF.Infrastructure;

public sealed record ClientCredentialsTokenRequest
{
    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }

    public string? Scope { get; init; }
}

public sealed record ClientCredentialsTokenResponse
{
    public required string AccessToken { get; init; }

    public required string TokenType { get; init; }

    public long? ExpiresIn { get; init; }

    public string? Scope { get; init; }
}

internal sealed record OAuthClientCredentialsTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    [JsonPropertyName("expires_in")]
    public long? ExpiresIn { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}
