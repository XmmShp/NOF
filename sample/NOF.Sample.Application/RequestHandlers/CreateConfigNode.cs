using NOF.Application;
using NOF.Contract;

namespace NOF.Sample.Application.RequestHandlers;

public class CreateConfigNode : IRequestHandler<CreateConfigNodeRequest>
{
    private readonly IConfigNodeRepository _configNodeRepository;
    private readonly IUnitOfWork _uow;

    public CreateConfigNode(IConfigNodeRepository configNodeRepository, IUnitOfWork uow)
    {
        _configNodeRepository = configNodeRepository;
        _uow = uow;
    }

    public async Task<Result> HandleAsync(CreateConfigNodeRequest request, CancellationToken cancellationToken)
    {
        var name = ConfigNodeName.Of(request.Name);
        var parentId = request.ParentId.HasValue ? ConfigNodeId.Of(request.ParentId.Value) : (ConfigNodeId?)null;

        if (await _configNodeRepository.ExistsByNameAsync(name, cancellationToken))
        {
            return Result.Fail(400, "Node with same name already exists.");
        }

        var node = ConfigNode.Create(name, parentId);
        _configNodeRepository.Add(node);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
