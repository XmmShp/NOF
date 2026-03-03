using System.Text.Json.Serialization;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Lightweight JWKS document model for HTTP deserialization.
/// Used instead of <see cref="Microsoft.IdentityModel.Tokens.JsonWebKey"/> to enable AOT-safe JSON deserialization.
/// </summary>
internal sealed record JwksHttpDocument
{
    [JsonPropertyName("keys")]
    public JwksHttpKey[] Keys { get; init; } = [];
}

/// <summary>
/// Lightweight JSON Web Key model for HTTP deserialization.
/// Contains only the fields needed for RSA key reconstruction.
/// </summary>
internal sealed record JwksHttpKey
{
    [JsonPropertyName("kty")]
    public string Kty { get; init; } = string.Empty;

    [JsonPropertyName("kid")]
    public string Kid { get; init; } = string.Empty;

    [JsonPropertyName("n")]
    public string N { get; init; } = string.Empty;

    [JsonPropertyName("e")]
    public string E { get; init; } = string.Empty;
}

/// <summary>
/// Source-generated JSON serializer context for AOT-safe JWKS HTTP deserialization.
/// </summary>
[JsonSerializable(typeof(JwksHttpDocument))]
internal sealed partial class JwksJsonContext : JsonSerializerContext;
