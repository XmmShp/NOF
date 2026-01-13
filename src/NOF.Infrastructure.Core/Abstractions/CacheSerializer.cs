namespace NOF;

public interface ICacheSerializer
{
    ReadOnlyMemory<byte> Serialize<T>(T value);

    T? Deserialize<T>(ReadOnlyMemory<byte> data);
}

public abstract class CacheSerializer : ICacheSerializer
{
    public abstract ReadOnlyMemory<byte> Serialize<T>(T value);

    public abstract T? Deserialize<T>(ReadOnlyMemory<byte> data);
}