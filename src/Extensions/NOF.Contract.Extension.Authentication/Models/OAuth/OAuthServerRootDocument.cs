using System.Text.Json.Serialization;

namespace NOF.Contract.Extension.Authentication;

public sealed record OAuthServerRootDocument
{
    [JsonPropertyName("issuer")]
    public required string Issuer { get; init; }

    [JsonPropertyName("metadata")]
    public required string Metadata { get; init; }
}
