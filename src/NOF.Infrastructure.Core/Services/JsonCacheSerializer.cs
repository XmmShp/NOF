using NOF.Contract;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Infrastructure.Abstraction;

public class JsonCacheSerializer : ICacheSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonCacheSerializer() : this(JsonSerializerOptions.NOF)
    {
    }

    public JsonCacheSerializer(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Serialize<T>(T value)
    {
        if (value is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var typeInfo = (JsonTypeInfo<T>)_options.GetTypeInfo(typeof(T));
        return JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
        {
            return default;
        }

        var typeInfo = (JsonTypeInfo<T>)_options.GetTypeInfo(typeof(T));
        return JsonSerializer.Deserialize(data.Span, typeInfo) is not { } value ? default : value;
    }
}

