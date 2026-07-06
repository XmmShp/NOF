using System.Text.Json.Serialization;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed record OAuthError
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }

    [JsonPropertyName("error_description")]
    public required string ErrorDescription { get; init; }
}

public sealed record OAuthServerMetadata
{
    [JsonPropertyName("issuer")]
    public required string Issuer { get; init; }

    [JsonPropertyName("authorization_endpoint")]
    public required string AuthorizationEndpoint { get; init; }

    [JsonPropertyName("token_endpoint")]
    public required string TokenEndpoint { get; init; }

    [JsonPropertyName("revocation_endpoint")]
    public string? RevocationEndpoint { get; init; }

    [JsonPropertyName("introspection_endpoint")]
    public string? IntrospectionEndpoint { get; init; }

    [JsonPropertyName("userinfo_endpoint")]
    public required string UserInfoEndpoint { get; init; }

    [JsonPropertyName("jwks_uri")]
    public required string JwksUri { get; init; }

    [JsonPropertyName("response_types_supported")]
    public required IReadOnlyList<string> ResponseTypesSupported { get; init; }

    [JsonPropertyName("grant_types_supported")]
    public required IReadOnlyList<string> GrantTypesSupported { get; init; }

    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public required IReadOnlyList<string> TokenEndpointAuthMethodsSupported { get; init; }

    [JsonPropertyName("revocation_endpoint_auth_methods_supported")]
    public IReadOnlyList<string>? RevocationEndpointAuthMethodsSupported { get; init; }

    [JsonPropertyName("introspection_endpoint_auth_methods_supported")]
    public IReadOnlyList<string>? IntrospectionEndpointAuthMethodsSupported { get; init; }

    [JsonPropertyName("subject_types_supported")]
    public required IReadOnlyList<string> SubjectTypesSupported { get; init; }

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public required IReadOnlyList<string> IdTokenSigningAlgValuesSupported { get; init; }

    [JsonPropertyName("code_challenge_methods_supported")]
    public required IReadOnlyList<string> CodeChallengeMethodsSupported { get; init; }

    [JsonPropertyName("scopes_supported")]
    public required IReadOnlyList<string> ScopesSupported { get; init; }

    [JsonPropertyName("claims_supported")]
    public required IReadOnlyList<string> ClaimsSupported { get; init; }
}

public sealed record OAuthServerRootDocument
{
    [JsonPropertyName("issuer")]
    public required string Issuer { get; init; }

    [JsonPropertyName("metadata")]
    public required string Metadata { get; init; }
}

public sealed record OAuthTokenEndpointResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public required string TokenType { get; init; }

    [JsonPropertyName("expires_in")]
    public required long ExpiresIn { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("scope")]
    public required string Scope { get; init; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; init; }
}

public sealed record OAuthIntrospectionResponse
{
    [JsonPropertyName("active")]
    public required bool Active { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("client_id")]
    public string? ClientId { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    [JsonPropertyName("sub")]
    public string? Subject { get; init; }

    [JsonPropertyName("exp")]
    public long? ExpiresAt { get; init; }

    [JsonPropertyName("iat")]
    public long? IssuedAt { get; init; }

    [JsonPropertyName("nbf")]
    public long? NotBefore { get; init; }

    [JsonPropertyName("iss")]
    public string? Issuer { get; init; }

    [JsonPropertyName("aud")]
    public string[]? Audience { get; init; }

    [JsonPropertyName("jti")]
    public string? TokenId { get; init; }

    [JsonExtensionData]
    public IDictionary<string, System.Text.Json.JsonElement>? AdditionalClaims { get; init; }
}
