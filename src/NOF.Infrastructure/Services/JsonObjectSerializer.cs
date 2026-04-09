using NOF.Abstraction;
using System.Text.Json;

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

    public ReadOnlyMemory<byte> Serialize(object? value, Type? runtimeType = null)
    {
        if (value is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        runtimeType ??= value.GetType();

        var typeInfo = Options.GetTypeInfo(runtimeType);
        return JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
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
}
