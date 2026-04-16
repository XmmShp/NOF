using NOF.Abstraction;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.EventHandlers;

/// <summary>
/// 节点创建时添加到子节点列表
/// </summary>
public class AddChildNodeOnConfigNodeCreated : InMemoryEventHandler<ConfigNodeCreatedEvent>
{
    private readonly IConfigNodeChildrenRepository _childrenRepository;

    public AddChildNodeOnConfigNodeCreated(IConfigNodeChildrenRepository childrenRepository)
    {
        _childrenRepository = childrenRepository;
    }

    public override async Task HandleAsync(ConfigNodeCreatedEvent @event, CancellationToken cancellationToken)
    {
        await _childrenRepository.AddChildNodeAsync(@event.Id, @event.ParentId, cancellationToken);
    }
}
