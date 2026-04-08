using NOF.Sample.Application.Entities;

namespace NOF.Sample.Application.Repositories;

public interface IConfigNodeChildrenRepository
{
    Task<ConfigNodeChildren?> GetChildrenAsync(ConfigNodeId id, CancellationToken cancellationToken = default);
    Task<bool> HasChildrenAsync(ConfigNodeId id, CancellationToken cancellationToken = default);
    Task AddChildNodeAsync(ConfigNodeId nodeId, ConfigNodeId? parentId, CancellationToken cancellationToken = default);
    Task RemoveChildNodeAsync(ConfigNodeId nodeId, ConfigNodeId? parentId, CancellationToken cancellationToken = default);
    Task UpdateChildNodeParentAsync(ConfigNodeId nodeId, ConfigNodeId? oldParentId, ConfigNodeId? newParentId, CancellationToken cancellationToken = default);
}
