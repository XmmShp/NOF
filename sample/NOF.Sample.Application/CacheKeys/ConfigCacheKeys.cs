using NOF.Application;

namespace NOF.Sample.Application.CacheKeys;

public record ConfigResultCacheKey(string AppName) : CacheKey<CachedConfigResult>($"ConfigResult:{AppName}");

public record CachedConfigResult(string Content, long Version);

public record ConfigNodeVersionCacheKey(ConfigNodeId Id) : CacheKey<long>($"ConfigNode:Version:{Id.Value}");

public record ConfigNodeByIdCacheKey(ConfigNodeId Id) : CacheKey<ConfigNodeDto>($"ConfigNode:{Id.Value}");

public record ConfigNodeByNameCacheKey(ConfigNodeName Name) : CacheKey<ConfigNodeDto>($"ConfigNode:ByName:{Name.Value}");
