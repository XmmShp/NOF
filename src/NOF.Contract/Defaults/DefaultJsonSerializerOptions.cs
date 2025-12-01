using System.Text.Json;
using System.Text.Json.Serialization;

namespace NOF;

public static class DefaultJsonSerializerOptions
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { OptionalConverterFactory.Instance },
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
