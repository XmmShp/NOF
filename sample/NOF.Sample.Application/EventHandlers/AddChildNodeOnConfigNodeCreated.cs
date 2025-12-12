using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.EventHandlers;

/// <summary>
/// 节点创建时更新父节点的子节点列表
/// </summary>
public class AddChildNodeOnConfigNodeCreated : IEventHandler<ConfigNodeCreatedEvent>
{
    private readonly IConfigNodeViewRepository _viewRepository;

    public AddChildNodeOnConfigNodeCreated(IConfigNodeViewRepository viewRepository)
    {
        _viewRepository = viewRepository;
    }

    public async Task HandleAsync(ConfigNodeCreatedEvent @event, CancellationToken cancellationToken)
    {
        await _viewRepository.AddChildNodeAsync(@event.Id, @event.ParentId, cancellationToken);
    }
}
