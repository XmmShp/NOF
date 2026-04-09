namespace NOF.Infrastructure;

public interface IObjectSerializer
{
    ReadOnlyMemory<byte> Serialize(object? value, Type? runtimeType = null);

    object? Deserialize(ReadOnlyMemory<byte> data, Type runtimeType);
}

public static class ObjectSerializerExtensions
{
    public static T? Deserialize<T>(this IObjectSerializer serializer, ReadOnlyMemory<byte> data)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        if (data.IsEmpty)
        {
            return default;
        }

        var obj = serializer.Deserialize(data, typeof(T));
        return obj is null ? default : (T)obj;
    }
}
