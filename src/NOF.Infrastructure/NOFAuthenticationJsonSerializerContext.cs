using System.Text.Json.Serialization;

namespace NOF.Infrastructure;

[JsonSerializable(typeof(JwksDocument))]
[JsonSerializable(typeof(JwkKeyDocument))]
[JsonSerializable(typeof(JwkKeyDocument[]))]
[JsonSerializable(typeof(OAuthAuthorizationServerMetadataDocument))]
[JsonSerializable(typeof(OAuthClientCredentialsTokenResponse))]
internal partial class NOFAuthenticationJsonSerializerContext : JsonSerializerContext;
