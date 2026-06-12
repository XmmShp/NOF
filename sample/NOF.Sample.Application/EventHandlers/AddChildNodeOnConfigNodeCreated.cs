using Microsoft.EntityFrameworkCore;
using NOF.Abstraction;

namespace NOF.Sample.Application.EventHandlers;

/// <summary>
/// 节点创建时添加到子节点列表
/// </summary>
public class AddChildNodeOnConfigNodeCreated : InMemoryEventHandler<ConfigNodeCreatedEvent>
{
    private readonly DbContext _dbContext;

    public AddChildNodeOnConfigNodeCreated(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task HandleAsync(ConfigNodeCreatedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.ParentId.HasValue)
        {
            var parentChildren = await _dbContext.Set<Entities.ConfigNodeChildren>()
                .FirstOrDefaultAsync(c => c.NodeId == @event.ParentId.Value, cancellationToken);

            if (parentChildren is null)
            {
                parentChildren = Entities.ConfigNodeChildren.Create(@event.ParentId.Value);
                _dbContext.Set<Entities.ConfigNodeChildren>().Add(parentChildren);
            }

            parentChildren.AddChild(@event.Id);
        }

        _dbContext.Set<Entities.ConfigNodeChildren>().Add(Entities.ConfigNodeChildren.Create(@event.Id));
    }
}
