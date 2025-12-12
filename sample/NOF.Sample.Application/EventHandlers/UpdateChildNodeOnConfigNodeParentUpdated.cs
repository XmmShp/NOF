using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.EventHandlers;

/// <summary>
/// 节点父节点更新时同步更新子节点列表
/// </summary>
public class UpdateChildNodeOnConfigNodeParentUpdated : IEventHandler<ConfigNodeParentUpdatedEvent>
{
    private readonly IConfigNodeViewRepository _viewRepository;

    public UpdateChildNodeOnConfigNodeParentUpdated(IConfigNodeViewRepository viewRepository)
    {
        _viewRepository = viewRepository;
    }

    public async Task HandleAsync(ConfigNodeParentUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await _viewRepository.UpdateChildNodeParentAsync(@event.NodeId, @event.OldParentId, @event.NewParentId, cancellationToken);
    }
}
