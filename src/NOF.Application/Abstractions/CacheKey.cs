namespace NOF;

public abstract class CacheKey
{
    public abstract string GenerateKey();
    public override string ToString() => GenerateKey();
    public static implicit operator string(CacheKey key)
    {
        return key.GenerateKey();
    }
    public override bool Equals(object? obj)
    {
        return obj is CacheKey other && GenerateKey() == other.GenerateKey();
    }
    public override int GetHashCode()
    {
        return GenerateKey().GetHashCode();
    }
}