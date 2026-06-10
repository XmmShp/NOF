using System.Text.Json.Serialization;

namespace NOF.Contract.Extension.Authentication;

/// <summary>
/// Standard JWKS (JSON Web Key Set) response model for the /.well-known/jwks.json endpoint.
/// </summary>
public sealed record JwksDocument
{
    [JsonPropertyName("keys")]
    public JwkKeyDocument[] Keys { get; init; } = [];
}

/// <summary>
/// Minimal JSON Web Key payload used by NOF for JWKS exchange.
/// </summary>
public sealed record JwkKeyDocument
{
    [JsonPropertyName("kid")]
    public string Kid { get; init; } = string.Empty;

    [JsonPropertyName("kty")]
    public string Kty { get; init; } = string.Empty;

    [JsonPropertyName("use")]
    public string Use { get; init; } = string.Empty;

    [JsonPropertyName("alg")]
    public string Alg { get; init; } = string.Empty;

    [JsonPropertyName("n")]
    public string N { get; init; } = string.Empty;

    [JsonPropertyName("e")]
    public string E { get; init; } = string.Empty;
}
