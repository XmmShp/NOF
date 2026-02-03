using System.Text.Json.Serialization;

namespace NOF;

/// <summary>
/// Represents a JSON Web Key (JWK) for JWT validation.
/// </summary>
public record JsonWebKey
{
    /// <summary>
    /// The Key Type parameter.
    /// </summary>
    [JsonPropertyName("kty")]
    public string Kty { get; init; } = string.Empty;

    /// <summary>
    /// The Key Use parameter.
    /// </summary>
    [JsonPropertyName("use")]
    public string Use { get; init; } = string.Empty;

    /// <summary>
    /// The Algorithm parameter.
    /// </summary>
    [JsonPropertyName("alg")]
    public string Alg { get; init; } = string.Empty;

    /// <summary>
    /// The Key ID parameter.
    /// </summary>
    [JsonPropertyName("kid")]
    public string Kid { get; init; } = string.Empty;

    /// <summary>
    /// The RSA Modulus parameter.
    /// </summary>
    [JsonPropertyName("n")]
    public string N { get; init; } = string.Empty;

    /// <summary>
    /// The RSA Exponent parameter.
    /// </summary>
    [JsonPropertyName("e")]
    public string E { get; init; } = string.Empty;
}
