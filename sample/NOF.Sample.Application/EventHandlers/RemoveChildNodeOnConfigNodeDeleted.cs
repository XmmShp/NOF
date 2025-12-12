using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.EventHandlers;

/// <summary>
/// 节点删除时从父节点的子节点列表中移除
/// </summary>
public class RemoveChildNodeOnConfigNodeDeleted : IEventHandler<ConfigNodeDeletedEvent>
{
    private readonly IConfigNodeViewRepository _viewRepository;

    public RemoveChildNodeOnConfigNodeDeleted(IConfigNodeViewRepository viewRepository)
    {
        _viewRepository = viewRepository;
    }

    public async Task HandleAsync(ConfigNodeDeletedEvent @event, CancellationToken cancellationToken)
    {
        await _viewRepository.RemoveChildNodeAsync(@event.Id, @event.ParentId, cancellationToken);
    }
}
