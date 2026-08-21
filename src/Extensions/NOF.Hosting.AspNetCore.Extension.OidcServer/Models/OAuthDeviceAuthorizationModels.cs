using System.Text.Json.Serialization;

namespace NOF.Hosting.AspNetCore.Extension.OidcServer;

public sealed record OAuthDeviceAuthorizationResponse
{
    [JsonPropertyName("device_code")]
    public required string DeviceCode { get; init; }

    [JsonPropertyName("user_code")]
    public required string UserCode { get; init; }

    [JsonPropertyName("verification_uri")]
    public required string VerificationUri { get; init; }

    [JsonPropertyName("verification_uri_complete")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VerificationUriComplete { get; init; }

    [JsonPropertyName("expires_in")]
    public required long ExpiresIn { get; init; }

    [JsonPropertyName("interval")]
    public required long Interval { get; init; }
}

public sealed record CreateOAuthDeviceGrantRequest
{
    public required string ClientId { get; init; }

    public required string ClientDisplayName { get; init; }

    public string? ClientLogoUri { get; init; }

    public required IReadOnlySet<string> Scopes { get; init; }
}

public sealed record OAuthDeviceAuthorizationDescriptor
{
    public required string UserCode { get; init; }

    public required string ClientId { get; init; }

    public required string ClientDisplayName { get; init; }

    public string? ClientLogoUri { get; init; }

    public required IReadOnlyList<string> Scopes { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }
}
