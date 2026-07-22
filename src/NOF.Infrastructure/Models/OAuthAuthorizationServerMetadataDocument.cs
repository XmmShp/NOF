using System.Text.Json.Serialization;

namespace NOF.Infrastructure;

public sealed record OAuthAuthorizationServerMetadataDocument
{
    [JsonPropertyName("issuer")]
    public string? Issuer { get; init; }

    [JsonPropertyName("token_endpoint")]
    public string? TokenEndpoint { get; init; }

    [JsonPropertyName("introspection_endpoint")]
    public string? IntrospectionEndpoint { get; init; }

    [JsonPropertyName("jwks_uri")]
    public string? JwksUri { get; init; }
}
