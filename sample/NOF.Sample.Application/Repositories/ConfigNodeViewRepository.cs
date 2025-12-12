using NOF.Sample.Application.CacheKeys;
using NOF.Sample.Application.Entities;

namespace NOF.Sample.Application.Repositories;

public interface IConfigNodeViewRepository
{
    Task<ConfigNodeDto?> GetByIdAsync(ConfigNodeId id, CancellationToken cancellationToken = default);
    Task<ConfigNodeDto?> GetByNameAsync(ConfigNodeName name, CancellationToken cancellationToken = default);
    Task<List<ConfigNodeDto>> GetRootNodesAsync(CancellationToken cancellationToken = default);
    Task<ConfigNodeChildren?> GetChildrenAsync(ConfigNodeId id, CancellationToken cancellationToken = default);
    Task<bool> HasChildrenAsync(ConfigNodeId id, CancellationToken cancellationToken = default);

    // 子节点管理方法
    Task AddChildNodeAsync(ConfigNodeId nodeId, ConfigNodeId? parentId, CancellationToken cancellationToken = default);
    Task RemoveChildNodeAsync(ConfigNodeId nodeId, ConfigNodeId? parentId, CancellationToken cancellationToken = default);
    Task UpdateChildNodeParentAsync(ConfigNodeId nodeId, ConfigNodeId? oldParentId, ConfigNodeId? newParentId, CancellationToken cancellationToken = default);
}
