using Microsoft.EntityFrameworkCore;
using NOF.Abstraction;

namespace NOF.Sample.Application.EventHandlers;

/// <summary>
/// 节点删除时从父节点的子节点列表中移除
/// </summary>
public class RemoveChildNodeOnConfigNodeDeleted : InMemoryEventHandler<ConfigNodeDeletedEvent>
{
    private readonly DbContext _dbContext;

    public RemoveChildNodeOnConfigNodeDeleted(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task HandleAsync(ConfigNodeDeletedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.ParentId.HasValue)
        {
            var parentChildren = await _dbContext.Set<Entities.ConfigNodeChildren>()
                .FirstOrDefaultAsync(c => c.NodeId == @event.ParentId.Value, cancellationToken);
            parentChildren?.RemoveChild(@event.Id);
        }

        var nodeChildren = await _dbContext.Set<Entities.ConfigNodeChildren>()
            .FirstOrDefaultAsync(c => c.NodeId == @event.Id, cancellationToken);

        if (nodeChildren is not null)
        {
            _dbContext.Set<Entities.ConfigNodeChildren>().Remove(nodeChildren);
        }
    }
}
