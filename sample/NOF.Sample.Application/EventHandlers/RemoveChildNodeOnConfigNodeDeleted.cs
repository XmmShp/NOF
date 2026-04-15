using NOF.Abstraction;
using NOF.Application;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.EventHandlers;

/// <summary>
/// 节点删除时从父节点的子节点列表中移除
/// </summary>
public class RemoveChildNodeOnConfigNodeDeleted : IEventHandler<ConfigNodeDeletedEvent>
{
    private readonly IConfigNodeChildrenRepository _childrenRepository;

    public RemoveChildNodeOnConfigNodeDeleted(IConfigNodeChildrenRepository childrenRepository)
    {
        _childrenRepository = childrenRepository;
    }

    public async Task HandleAsync(ConfigNodeDeletedEvent @event, CancellationToken cancellationToken)
    {
        await _childrenRepository.RemoveChildNodeAsync(@event.Id, @event.ParentId, cancellationToken);
    }
}
