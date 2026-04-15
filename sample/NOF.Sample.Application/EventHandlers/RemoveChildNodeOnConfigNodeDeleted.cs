using NOF.Abstraction;
using NOF.Application;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.EventHandlers;

/// <summary>
/// 节点删除时从父节点的子节点列表中移除
/// </summary>
public class RemoveChildNodeOnConfigNodeDeleted : Abstraction.EventHandler<ConfigNodeDeletedEvent>
{
    private readonly IConfigNodeChildrenRepository _childrenRepository;

    public RemoveChildNodeOnConfigNodeDeleted(IConfigNodeChildrenRepository childrenRepository)
    {
        _childrenRepository = childrenRepository;
    }

    public override async Task HandleAsync(ConfigNodeDeletedEvent payload, CancellationToken cancellationToken)
    {
        await _childrenRepository.RemoveChildNodeAsync(payload.Id, payload.ParentId, cancellationToken);
    }
}
