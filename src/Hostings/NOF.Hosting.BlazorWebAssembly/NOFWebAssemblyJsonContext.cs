using System.Text.Json.Serialization;

namespace NOF.Hosting.BlazorWebAssembly;

[JsonSerializable(typeof(BrowserStorageCacheEntry))]
internal partial class NOFWebAssemblyJsonContext : JsonSerializerContext;
