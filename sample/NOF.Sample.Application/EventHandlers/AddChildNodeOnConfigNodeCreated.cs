using NOF.Abstraction;
using NOF.Application;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.EventHandlers;

/// <summary>
/// 节点创建时添加到子节点列表
/// </summary>
public class AddChildNodeOnConfigNodeCreated : IEventHandler<ConfigNodeCreatedEvent>
{
    private readonly IConfigNodeChildrenRepository _childrenRepository;

    public AddChildNodeOnConfigNodeCreated(IConfigNodeChildrenRepository childrenRepository)
    {
        _childrenRepository = childrenRepository;
    }

    public async Task HandleAsync(ConfigNodeCreatedEvent @event, CancellationToken cancellationToken)
    {
        await _childrenRepository.AddChildNodeAsync(@event.Id, @event.ParentId, cancellationToken);
    }
}
