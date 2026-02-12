using NOF.Contract;
using System.Text.Json;

namespace NOF.Infrastructure.Core;

public interface ICacheSerializer
{
    ReadOnlyMemory<byte> Serialize<T>(T value);

    T? Deserialize<T>(ReadOnlyMemory<byte> data);
}

public class JsonCacheSerializer : ICacheSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonCacheSerializer() : this(JsonSerializerOptions.NOFDefaults)
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

        return JsonSerializer.SerializeToUtf8Bytes(value, _options);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty || JsonSerializer.Deserialize<T>(data.Span, _options) is not { } value)
        {
            return default;
        }

        return value;
    }
}

