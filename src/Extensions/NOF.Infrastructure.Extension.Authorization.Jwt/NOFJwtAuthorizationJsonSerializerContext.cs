using System.Text.Json.Serialization;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

[JsonSerializable(typeof(JwksDocument))]
[JsonSerializable(typeof(JwkKeyDocument))]
[JsonSerializable(typeof(JwkKeyDocument[]))]
[JsonSerializable(typeof(OAuthTokenEndpointResponse))]
internal partial class NOFJwtAuthorizationJsonSerializerContext : JsonSerializerContext;
