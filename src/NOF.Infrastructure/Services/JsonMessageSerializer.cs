using NOF.Contract;
using System.Text.Json;

namespace NOF.Infrastructure;

public sealed class JsonMessageSerializer : JsonObjectSerializer, IMessageSerializer
{
    public JsonMessageSerializer() : this(JsonSerializerOptions.NOF)
    {
    }

    public JsonMessageSerializer(JsonSerializerOptions options) : base(options)
    {
    }

    public string Serialize(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var typeInfo = Options.GetTypeInfo(message.GetType());
        return JsonSerializer.Serialize(message, typeInfo);
    }

    public IMessage Deserialize(string payloadType, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadType);
        ArgumentNullException.ThrowIfNull(payload);

        var type = TypeRegistry.Resolve(payloadType);
        var typeInfo = Options.GetTypeInfo(type);
        return (IMessage)JsonSerializer.Deserialize(payload, typeInfo)!;
    }
}
