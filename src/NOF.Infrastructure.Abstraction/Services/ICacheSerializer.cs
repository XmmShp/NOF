namespace NOF.Infrastructure.Abstraction;

public interface ICacheSerializer
{
    ReadOnlyMemory<byte> Serialize<T>(T value);

    T? Deserialize<T>(ReadOnlyMemory<byte> data);
}
