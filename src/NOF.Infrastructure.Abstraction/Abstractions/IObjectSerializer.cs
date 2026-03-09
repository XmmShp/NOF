namespace NOF.Infrastructure.Abstraction;

public interface IObjectSerializer
{
    ReadOnlyMemory<byte> Serialize<T>(T value);

    T? Deserialize<T>(ReadOnlyMemory<byte> data);
}
