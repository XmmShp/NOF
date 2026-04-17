using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

/// <summary>
/// Standard JWKS (JSON Web Key Set) response model for the /.well-known/jwks.json endpoint.
/// </summary>
public sealed record JwksDocument
{
    [JsonPropertyName("keys")]
    public JsonWebKey[] Keys { get; init; } = [];
}

