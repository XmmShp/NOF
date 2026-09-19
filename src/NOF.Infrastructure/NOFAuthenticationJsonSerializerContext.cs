using System.Text.Json.Serialization;

namespace NOF.Infrastructure;
/// <summary>Provides generated JSON metadata for authentication documents.</summary>

[JsonSerializable(typeof(JwksDocument))]
[JsonSerializable(typeof(JwkKeyDocument))]
[JsonSerializable(typeof(JwkKeyDocument[]))]
[JsonSerializable(typeof(OAuthAuthorizationServerMetadataDocument))]
public partial class NOFAuthenticationJsonSerializerContext : JsonSerializerContext;
