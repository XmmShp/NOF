using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.RequestHandlers;

public class DeleteConfigNode : IRequestHandler<DeleteConfigNodeRequest>
{
    private readonly IConfigNodeRepository _configNodeRepository;
    private readonly IConfigNodeViewRepository _configNodeViewRepository;
    private readonly IUnitOfWork _uow;

    public DeleteConfigNode(
        IConfigNodeRepository configNodeRepository,
        IConfigNodeViewRepository configNodeViewRepository,
        IUnitOfWork uow)
    {
        _configNodeRepository = configNodeRepository;
        _configNodeViewRepository = configNodeViewRepository;
        _uow = uow;
    }

    public async Task<Result> HandleAsync(DeleteConfigNodeRequest request, CancellationToken cancellationToken)
    {
        var id = ConfigNodeId.From(request.Id);
        var node = await _configNodeRepository.FindAsync(id, cancellationToken);
        if (node is null)
        {
            return Result.Fail(404, "Node not found.");
        }

        if (await _configNodeViewRepository.HasChildrenAsync(id, cancellationToken))
        {
            return Result.Fail(400, "Cannot delete node with children.");
        }

        node.MarkAsDeleted();
        _configNodeRepository.Remove(node);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
