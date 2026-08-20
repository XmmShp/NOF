using System.Text.Json;
using System.Text.Json.Serialization;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public record OAuthClientRegistrationRequest
{
    [JsonPropertyName("redirect_uris")]
    public string[]? RedirectUris { get; init; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthenticationMethod { get; init; }

    [JsonPropertyName("grant_types")]
    public string[]? GrantTypes { get; init; }

    [JsonPropertyName("response_types")]
    public string[]? ResponseTypes { get; init; }

    [JsonPropertyName("client_name")]
    public string? ClientName { get; init; }

    [JsonPropertyName("client_uri")]
    public string? ClientUri { get; init; }

    [JsonPropertyName("logo_uri")]
    public string? LogoUri { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("contacts")]
    public string[]? Contacts { get; init; }

    [JsonPropertyName("tos_uri")]
    public string? TermsOfServiceUri { get; init; }

    [JsonPropertyName("policy_uri")]
    public string? PolicyUri { get; init; }

    [JsonPropertyName("jwks")]
    public JsonElement? JsonWebKeySet { get; init; }

    [JsonPropertyName("software_id")]
    public string? SoftwareId { get; init; }

    [JsonPropertyName("software_version")]
    public string? SoftwareVersion { get; init; }

    [JsonPropertyName("application_type")]
    public string? ApplicationType { get; init; }

    [JsonPropertyName("subject_type")]
    public string? SubjectType { get; init; }

    [JsonPropertyName("id_token_signed_response_alg")]
    public string? IdTokenSignedResponseAlgorithm { get; init; }
}

public sealed record OAuthClientRegistrationUpdateRequest : OAuthClientRegistrationRequest
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; init; } = string.Empty;

    [JsonPropertyName("client_secret")]
    public string? ClientSecret { get; init; }
}

public sealed record OAuthClientRegistrationResponse
{
    [JsonPropertyName("registration_access_token")]
    public required string RegistrationAccessToken { get; init; }

    [JsonPropertyName("registration_client_uri")]
    public required string RegistrationClientUri { get; init; }

    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

    [JsonPropertyName("client_secret")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientSecret { get; init; }

    [JsonPropertyName("client_id_issued_at")]
    public required long ClientIdIssuedAt { get; init; }

    [JsonPropertyName("client_secret_expires_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ClientSecretExpiresAt { get; init; }

    [JsonPropertyName("redirect_uris")]
    public required IReadOnlyList<string> RedirectUris { get; init; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public required string TokenEndpointAuthenticationMethod { get; init; }

    [JsonPropertyName("grant_types")]
    public required IReadOnlyList<string> GrantTypes { get; init; }

    [JsonPropertyName("response_types")]
    public required IReadOnlyList<string> ResponseTypes { get; init; }

    [JsonPropertyName("client_name")]
    public required string ClientName { get; init; }

    [JsonPropertyName("client_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientUri { get; init; }

    [JsonPropertyName("logo_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LogoUri { get; init; }

    [JsonPropertyName("scope")]
    public required string Scope { get; init; }

    [JsonPropertyName("contacts")]
    public required IReadOnlyList<string> Contacts { get; init; }

    [JsonPropertyName("tos_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TermsOfServiceUri { get; init; }

    [JsonPropertyName("policy_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyUri { get; init; }

    [JsonPropertyName("jwks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? JsonWebKeySet { get; init; }

    [JsonPropertyName("software_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SoftwareId { get; init; }

    [JsonPropertyName("software_version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SoftwareVersion { get; init; }

    [JsonPropertyName("application_type")]
    public required string ApplicationType { get; init; }

    [JsonPropertyName("subject_type")]
    public required string SubjectType { get; init; }

    [JsonPropertyName("id_token_signed_response_alg")]
    public required string IdTokenSignedResponseAlgorithm { get; init; }
}

internal sealed record OAuthClientStoredRegistrationMetadata
{
    public string SubjectType { get; init; } = OAuthSubjectTypes.Public;

    public string IdTokenSignedResponseAlgorithm { get; init; } = OAuthSigningAlgorithms.RsaSha256;

    public string? ClientUri { get; init; }

    public string? LogoUri { get; init; }

    public IReadOnlyList<string> Contacts { get; init; } = [];

    public string? TermsOfServiceUri { get; init; }

    public string? PolicyUri { get; init; }

    public string? SoftwareId { get; init; }

    public string? SoftwareVersion { get; init; }
}
