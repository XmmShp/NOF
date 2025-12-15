using Microsoft.Extensions.Caching.Distributed;
using NOF;
using NOF.Sample.Application.CacheKeys;
using NOF.Sample.Application.Repositories;
using System.Text.Json.Nodes;

namespace NOF.Sample.Application.RequestHandlers;

public class GetConfiguration : IRequestHandler<GetConfigurationRequest, GetConfigurationResponse>
{
    private readonly IConfigNodeViewRepository _viewRepository;
    private readonly IDistributedCache _cache;

    public GetConfiguration(IConfigNodeViewRepository viewRepository, IDistributedCache cache)
    {
        _viewRepository = viewRepository;
        _cache = cache;
    }

    public async Task<Result<GetConfigurationResponse>> HandleAsync(GetConfigurationRequest request, CancellationToken cancellationToken)
    {
        var appNameStr = request.AppName;
        var appCacheKey = new ConfigResultCacheKey(appNameStr);

        // 1. Find App Node
        var appName = ConfigNodeName.From(appNameStr);
        var appNode = await _viewRepository.GetByNameAsync(appName, cancellationToken);

        if (appNode is null)
        {
            return Result.Fail(404, "配置节点不存在");
        }

        // 2. Expand Path to Root
        var fullPath = await ExpandPathToRoot(appNode, cancellationToken);

        // Check versions
        var maxVersion = 0L;
        foreach (var versionKey in fullPath.Select(node => new ConfigNodeVersionCacheKey(ConfigNodeId.From(node.Id))))
        {
            var (success, versionVal) = await _cache.TryGetAsync(versionKey, cancellationToken: cancellationToken);
            if (success && versionVal > maxVersion)
            {
                maxVersion = versionVal;
            }
        }

        var (isSuccess, cachedResult) = await _cache.TryGetAsync(appCacheKey, cancellationToken: cancellationToken);
        if (isSuccess && cachedResult!.Version >= maxVersion)
        {
            return new GetConfigurationResponse(cachedResult.Content);
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
            token: cancellationToken);

        return new GetConfigurationResponse(jsonString);
    }

    private async Task<List<ConfigNodeDto>> ExpandPathToRoot(ConfigNodeDto appNode, CancellationToken cancellationToken)
    {
        var fullPath = new List<ConfigNodeDto>();

        // Trace up from AppNode
        var current = appNode;
        fullPath.Add(current);

        while (current.ParentId.HasValue)
        {
            var parent = await _viewRepository.GetByIdAsync(ConfigNodeId.From(current.ParentId.Value), cancellationToken);
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

