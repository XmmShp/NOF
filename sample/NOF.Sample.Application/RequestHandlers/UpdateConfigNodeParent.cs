using NOF.Application;
using NOF.Contract;

namespace NOF.Sample.Application.RequestHandlers;

public class UpdateConfigNodeParent : NOFSampleService.UpdateConfigNodeParent
{
    private readonly IConfigNodeRepository _configNodeRepository;
    private readonly IUnitOfWork _uow;

    public UpdateConfigNodeParent(IConfigNodeRepository configNodeRepository, IUnitOfWork uow)
    {
        _configNodeRepository = configNodeRepository;
        _uow = uow;
    }

    public async Task<Result> UpdateConfigNodeParentAsync(UpdateConfigNodeParentRequest request, CancellationToken cancellationToken)
    {
        var nodeId = ConfigNodeId.Of(request.NodeId);
        var node = await _configNodeRepository.FindAsync(nodeId, cancellationToken);

        if (node is null)
        {
            return Result.Fail("404", "Node not found.");
        }

        var newParentId = request.NewParentId.HasValue
            ? ConfigNodeId.Of(request.NewParentId.Value)
            : (ConfigNodeId?)null;

        // Check whether the target parent exists (unless moving to root).
        if (newParentId.HasValue)
        {
            var parentNode = await _configNodeRepository.FindAsync(newParentId.Value, cancellationToken);
            if (parentNode is null)
            {
                return Result.Fail("404", "Target parent node not found.");
            }

            // Prevent cyclic parent relationship.
            if (await IsDescendant(nodeId, newParentId.Value, cancellationToken))
            {
                return Result.Fail("400", "Cannot move a node under its descendant.");
            }
        }

        node.UpdateParent(newParentId);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<bool> IsDescendant(ConfigNodeId ancestorId, ConfigNodeId nodeId, CancellationToken cancellationToken)
    {
        var current = await _configNodeRepository.FindAsync(nodeId, cancellationToken);

        while (current?.ParentId != null)
        {
            if (current.ParentId == ancestorId)
            {
                return true;
            }
            current = await _configNodeRepository.FindAsync(current.ParentId.Value, cancellationToken);
        }

        return false;
    }
}
