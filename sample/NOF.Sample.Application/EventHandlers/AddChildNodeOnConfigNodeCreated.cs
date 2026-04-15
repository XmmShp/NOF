using NOF.Abstraction;
using NOF.Application;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.EventHandlers;

/// <summary>
/// 节点创建时添加到子节点列表
/// </summary>
public class AddChildNodeOnConfigNodeCreated : NOF.Abstraction.EventHandler<ConfigNodeCreatedEvent>
{
    private readonly IConfigNodeChildrenRepository _childrenRepository;

    public AddChildNodeOnConfigNodeCreated(IConfigNodeChildrenRepository childrenRepository)
    {
        _childrenRepository = childrenRepository;
    }

    public override async Task HandleAsync(ConfigNodeCreatedEvent payload, CancellationToken cancellationToken)
    {
        await _childrenRepository.AddChildNodeAsync(payload.Id, payload.ParentId, cancellationToken);
    }
}
