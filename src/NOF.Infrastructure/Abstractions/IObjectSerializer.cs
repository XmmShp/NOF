namespace NOF.Infrastructure;

public interface IObjectSerializer
{
    ReadOnlyMemory<byte> Serialize<T>(T value);

    T? Deserialize<T>(ReadOnlyMemory<byte> data);
}
