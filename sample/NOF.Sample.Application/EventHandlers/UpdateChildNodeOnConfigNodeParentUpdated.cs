using NOF.Application.Data;
using NOF.Abstraction;
using NOF.Application;

namespace NOF.Sample.Application.EventHandlers;

/// <summary>
/// 节点父节点更新时同步更新子节点列表
/// </summary>
public class UpdateChildNodeOnConfigNodeParentUpdated : InMemoryEventHandler<ConfigNodeParentUpdatedEvent>
{
    private readonly IDbContext _dbContext;

    public UpdateChildNodeOnConfigNodeParentUpdated(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task HandleAsync(ConfigNodeParentUpdatedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.OldParentId.HasValue)
        {
            var oldParentChildren = await _dbContext.Set<Entities.ConfigNodeChildren>()
                .FirstOrDefaultAsync(c => c.NodeId == @event.OldParentId.Value, cancellationToken);
            oldParentChildren?.RemoveChild(@event.NodeId);
        }

        if (@event.NewParentId.HasValue)
        {
            var newParentChildren = await _dbContext.Set<Entities.ConfigNodeChildren>()
                .FirstOrDefaultAsync(c => c.NodeId == @event.NewParentId.Value, cancellationToken);

            if (newParentChildren is null)
            {
                newParentChildren = Entities.ConfigNodeChildren.Create(@event.NewParentId.Value);
                _dbContext.Set<Entities.ConfigNodeChildren>().Add(newParentChildren);
            }

            newParentChildren.AddChild(@event.NodeId);
        }
    }
}
