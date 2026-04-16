using NOF.Abstraction;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.EventHandlers;

/// <summary>
/// 节点父节点更新时同步更新子节点列表
/// </summary>
public class UpdateChildNodeOnConfigNodeParentUpdated : InMemoryEventHandler<ConfigNodeParentUpdatedEvent>
{
    private readonly IConfigNodeChildrenRepository _childrenRepository;

    public UpdateChildNodeOnConfigNodeParentUpdated(IConfigNodeChildrenRepository childrenRepository)
    {
        _childrenRepository = childrenRepository;
    }

    public override async Task HandleAsync(ConfigNodeParentUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await _childrenRepository.UpdateChildNodeParentAsync(@event.NodeId, @event.OldParentId, @event.NewParentId, cancellationToken);
    }
}
