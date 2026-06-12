using System.Text.Json.Serialization;
using NOF.Contract.Extension.Authentication;

namespace NOF.Infrastructure.Extension.Authentication;

[JsonSerializable(typeof(JwksDocument))]
[JsonSerializable(typeof(JwkKeyDocument))]
[JsonSerializable(typeof(JwkKeyDocument[]))]
internal partial class NOFAuthenticationJsonSerializerContext : JsonSerializerContext;
