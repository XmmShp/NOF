using System.Text.Json.Serialization;
using NOF.Hosting.AspNetCore.Extension.OidcServer;

namespace NOF.Infrastructure.Extension.Authentication;

[JsonSerializable(typeof(JwksDocument))]
[JsonSerializable(typeof(JwkKeyDocument))]
[JsonSerializable(typeof(JwkKeyDocument[]))]
internal partial class NOFAuthenticationJsonSerializerContext : JsonSerializerContext;
