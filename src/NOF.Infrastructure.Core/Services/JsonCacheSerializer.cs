using NOF.Contract;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace NOF.Infrastructure.Abstraction;

public class JsonCacheSerializer : ICacheSerializer
{
    private readonly JsonSerializerOptions _options;

    [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "NOFDefaults is intentionally used as fallback; callers can provide AOT-safe options.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "NOFDefaults is intentionally used as fallback; callers can provide AOT-safe options.")]
    public JsonCacheSerializer() : this(JsonSerializerOptions.NOFDefaults)
    {
    }

    public JsonCacheSerializer(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "Serialization uses pre-configured options; types are preserved by the application.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Serialization uses pre-configured options; types are preserved by the application.")]
    public ReadOnlyMemory<byte> Serialize<T>(T value)
    {
        if (value is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        return JsonSerializer.SerializeToUtf8Bytes(value, _options);
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "Deserialization uses pre-configured options; types are preserved by the application.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Deserialization uses pre-configured options; types are preserved by the application.")]
    public T? Deserialize<T>(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty || JsonSerializer.Deserialize<T>(data.Span, _options) is not { } value)
        {
            return default;
        }

        return value;
    }
}

