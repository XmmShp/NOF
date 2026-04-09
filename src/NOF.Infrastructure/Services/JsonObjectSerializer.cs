using NOF.Contract;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Infrastructure;

public class JsonObjectSerializer : IObjectSerializer
{
    protected JsonSerializerOptions Options { get; }

    public JsonObjectSerializer() : this(JsonSerializerOptions.NOF)
    {
    }

    public JsonObjectSerializer(JsonSerializerOptions options)
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

    public ReadOnlyMemory<byte> Serialize(object value, Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(runtimeType);

        var typeInfo = Options.GetTypeInfo(runtimeType);
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

    public object? Deserialize(ReadOnlyMemory<byte> data, Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);

        if (data.IsEmpty)
        {
            return default;
        }

        var typeInfo = Options.GetTypeInfo(runtimeType);
        return JsonSerializer.Deserialize(data.Span, typeInfo);
    }

    public string SerializeToString(object value, Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(runtimeType);

        var typeInfo = Options.GetTypeInfo(runtimeType);
        return JsonSerializer.Serialize(value, typeInfo);
    }

    public object? Deserialize(string data, Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(runtimeType);

        if (string.IsNullOrEmpty(data))
        {
            return default;
        }

        var typeInfo = Options.GetTypeInfo(runtimeType);
        return JsonSerializer.Deserialize(data, typeInfo);
    }
}
