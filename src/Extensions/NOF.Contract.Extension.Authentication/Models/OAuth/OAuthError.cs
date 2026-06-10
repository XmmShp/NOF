using System.Text.Json.Serialization;

namespace NOF.Contract.Extension.Authentication;

public sealed record OAuthError
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }

    [JsonPropertyName("error_description")]
    public required string ErrorDescription { get; init; }
}
