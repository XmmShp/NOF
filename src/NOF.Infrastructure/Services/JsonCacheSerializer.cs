using NOF.Contract;
using System.Text.Json;

namespace NOF.Infrastructure;

public class JsonCacheSerializer : JsonObjectSerializer, ICacheSerializer
{
    public JsonCacheSerializer() : this(JsonSerializerOptions.NOF)
    {
    }

    public JsonCacheSerializer(JsonSerializerOptions options) : base(options)
    {
    }
}

