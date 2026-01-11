using System.Text.Json;

namespace NOF.Caching;

public interface ICacheSerializer
{
    ReadOnlyMemory<byte> Serialize<T>(T value);

    T? Deserialize<T>(ReadOnlyMemory<byte> data);
}

public abstract class CacheSerializer : ICacheSerializer
{
    public abstract ReadOnlyMemory<byte> Serialize<T>(T value);

    public abstract T? Deserialize<T>(ReadOnlyMemory<byte> data);
}

public class JsonCacheSerializer : CacheSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonCacheSerializer() : this(DefaultJsonSerializerOptions.Options)
    {
    }

    public JsonCacheSerializer(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> Serialize<T>(T value)
    {
        if (value is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        return JsonSerializer.SerializeToUtf8Bytes(value, _options);
    }

    /// <inheritdoc />
    public override T? Deserialize<T>(ReadOnlyMemory<byte> data) where T : default
    {
        if (data.IsEmpty || JsonSerializer.Deserialize<T>(data.Span, _options) is not { } value)
        {
            return default;
        }

        return value;
    }
}
