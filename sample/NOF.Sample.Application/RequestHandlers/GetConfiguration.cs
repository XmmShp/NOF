using Microsoft.Extensions.Caching.Distributed;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using NOF.Sample.Application.CacheKeys;
using System.Text.Json.Nodes;

namespace NOF.Sample.Application.RequestHandlers;

public class GetConfiguration : NOFSampleService.GetConfiguration
{
    private readonly IRepository<ConfigNode, ConfigNodeId> _configNodeRepository;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;

    public GetConfiguration(IRepository<ConfigNode, ConfigNodeId> configNodeRepository, ICacheService cache, IMapper mapper)
    {
        _configNodeRepository = configNodeRepository;
        _cache = cache;
        _mapper = mapper;
    }

    public async Task<Result<GetConfigurationResponse>> GetConfigurationAsync(GetConfigurationRequest request)
    {
        var cancellationToken = CancellationToken.None;
        var appNameStr = request.AppName;
        var appCacheKey = new ConfigResultCacheKey(appNameStr);

        // 1. Find App Node
        var appName = ConfigNodeName.Of(appNameStr);
        var appNodeDto = await GetNodeByNameAsync(appName, cancellationToken);

        if (appNodeDto is null)
        {
            return Result.Fail("404", "Config node not found.");
        }

        // 2. Expand Path to Root
        var fullPath = await ExpandPathToRoot(appNodeDto, cancellationToken);

        // Check versions
        var maxVersion = 0L;
        foreach (var versionKey in fullPath.Select(node => new ConfigNodeVersionCacheKey(ConfigNodeId.Of(node.Id))))
        {
            var version = await _cache.GetAsync(versionKey, cancellationToken: cancellationToken);
            version.IfSome(versionVal =>
            {
                if (versionVal > maxVersion)
                {
                    maxVersion = versionVal;
                }
            });
        }

        var cachedResult = await _cache.GetAsync(appCacheKey, cancellationToken: cancellationToken);
        if (cachedResult.HasValue && cachedResult.Value.Version >= maxVersion)
        {
            return new GetConfigurationResponse
            {
                Content = cachedResult.Value.Content
            };
        }


        // 3. Re-compute
        var mergedJson = ComputeConfiguration(fullPath);
        var jsonString = mergedJson.ToJsonString();

        // 4. Update Result Cache
        var newResult = new CachedConfigResult(jsonString, maxVersion);
        await _cache.SetAsync(
            appCacheKey,
            newResult,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) },
            cancellationToken);

        return new GetConfigurationResponse
        {
            Content = jsonString
        };
    }

    private async Task<ConfigNodeDto?> GetNodeByNameAsync(ConfigNodeName name, CancellationToken cancellationToken)
    {
        var cacheKey = new ConfigNodeByNameCacheKey(name);
        var cachedValue = await _cache.GetAsync(cacheKey, cancellationToken: cancellationToken);
        if (cachedValue.HasValue)
        {
            return cachedValue.Value;
        }

        var node = await _configNodeRepository.GetNodeByNameAsync(name, cancellationToken);
        if (node is null)
        {
            return null;
        }

        var dto = _mapper.Map<ConfigNode, ConfigNodeDto>(node);
        await _cache.SetAsync(
            cacheKey,
            dto,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) },
            cancellationToken);

        return dto;
    }

    private async Task<ConfigNodeDto?> GetNodeByIdAsync(ConfigNodeId id, CancellationToken cancellationToken)
    {
        var cacheKey = new ConfigNodeByIdCacheKey(id);
        var cachedValue = await _cache.GetAsync(cacheKey, cancellationToken: cancellationToken);
        if (cachedValue.HasValue)
        {
            return cachedValue.Value;
        }

        var node = await _configNodeRepository.GetNodeByIdAsync(id, cancellationToken);
        if (node is null)
        {
            return null;
        }

        var dto = _mapper.Map<ConfigNode, ConfigNodeDto>(node);
        await _cache.SetAsync(
            cacheKey,
            dto,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) },
            cancellationToken);

        return dto;
    }

    private async Task<List<ConfigNodeDto>> ExpandPathToRoot(ConfigNodeDto appNode, CancellationToken cancellationToken)
    {
        var fullPath = new List<ConfigNodeDto>();

        // Trace up from AppNode
        var current = appNode;
        fullPath.Add(current);

        while (current.ParentId.HasValue)
        {
            var parent = await GetNodeByIdAsync(ConfigNodeId.Of(current.ParentId.Value), cancellationToken);
            if (parent is null)
            {
                break;
            }
            fullPath.Add(parent);
            current = parent;
        }

        fullPath.Reverse();
        return fullPath;
    }

    private static JsonObject ComputeConfiguration(List<ConfigNodeDto> pathNodes)
    {
        var mergedJson = new JsonObject();

        // PathNodes is ordered from Root -> App
        foreach (var activeFile in pathNodes
                     .Where(node => !string.IsNullOrWhiteSpace(node.ActiveFileName))
                     .Select(node => node.ConfigFiles.FirstOrDefault(f => f.Name == node.ActiveFileName))
                     .Where(activeFile => !string.IsNullOrWhiteSpace(activeFile?.Content)))
        {
            var nodeJson = JsonNode.Parse(activeFile!.Content);
            if (nodeJson is JsonObject jsonObj)
            {
                MergeJson(mergedJson, jsonObj);
            }
        }
        return mergedJson;
    }

    private static void MergeJson(JsonObject target, JsonObject source)
    {
        foreach (var (key, sourceValue) in source)
        {
            if (target.ContainsKey(key))
            {
                var targetValue = target[key];

                if (targetValue is JsonObject targetObj && sourceValue is JsonObject sourceObj)
                {
                    MergeJson(targetObj, sourceObj);
                }
                else
                {
                    // Overwrite
                    target[key] = sourceValue?.DeepClone();
                }
            }
            else
            {
                target.Add(key, sourceValue?.DeepClone());
            }
        }
    }
}





