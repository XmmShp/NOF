namespace NOF;

/// <summary>
/// Strongly-typed cache key associated with a value type.
/// </summary>
/// <typeparam name="TValue">The cached value type.</typeparam>
/// <param name="Key">The cache key string.</param>
public abstract record CacheKey<TValue>(string Key);