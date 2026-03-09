using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Infrastructure.Abstraction;

public abstract class JsonObjectSerializer : IObjectSerializer
{
    protected JsonSerializerOptions Options { get; }

    protected JsonObjectSerializer(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
    }

    public ReadOnlyMemory<byte> Serialize<T>(T value)
    {
        if (value is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var typeInfo = (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));
        return JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
    }

    public T? Deserialize<T>(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
        {
            return default;
        }

        var typeInfo = (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));
        return JsonSerializer.Deserialize(data.Span, typeInfo) ?? default;
    }
}
