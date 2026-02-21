using NOF.Application;
using NOF.Contract;

namespace NOF.Sample.Application.RequestHandlers;

public class AddOrUpdateConfigFile : IRequestHandler<AddOrUpdateConfigFileRequest>
{
    private readonly IConfigNodeRepository _configNodeRepository;
    private readonly IUnitOfWork _uow;

    public AddOrUpdateConfigFile(IConfigNodeRepository configNodeRepository, IUnitOfWork uow)
    {
        _configNodeRepository = configNodeRepository;
        _uow = uow;
    }

    public async Task<Result> HandleAsync(AddOrUpdateConfigFileRequest request, CancellationToken cancellationToken)
    {
        var id = ConfigNodeId.Of(request.NodeId);
        var node = await _configNodeRepository.FindAsync(id, cancellationToken);

        if (node is null)
        {
            return Result.Fail(404, "Node not found.");
        }

        var fileName = ConfigFileName.Of(request.FileName);
        var content = ConfigContent.Of(request.Content);

        node.AddOrUpdateConfigFile(fileName, content);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
