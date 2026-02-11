using System.Text.Json.Serialization;

namespace NOF;

/// <summary>
/// Standard JWKS (JSON Web Key Set) response model for the /.well-known/jwks.json endpoint.
/// </summary>
public record JwksDocument
{
    /// <summary>
    /// The set of JSON Web Keys.
    /// </summary>
    [JsonPropertyName("keys")]
    public JsonWebKey[] Keys { get; init; } = [];
}
