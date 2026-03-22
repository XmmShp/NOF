using System.Text.Json.Serialization;

namespace NOF.UI;

[JsonSerializable(typeof(BrowserStorageCacheEntry))]
public partial class NOFUIJsonContext : JsonSerializerContext;

