namespace NOF.Infrastructure;

public interface IObjectSerializer
{
    ReadOnlyMemory<byte> Serialize<T>(T value);

    ReadOnlyMemory<byte> Serialize(object value, Type runtimeType);

    T? Deserialize<T>(ReadOnlyMemory<byte> data);

    object? Deserialize(ReadOnlyMemory<byte> data, Type runtimeType);

    string SerializeToString(object value, Type runtimeType);

    object? Deserialize(string data, Type runtimeType);
}
