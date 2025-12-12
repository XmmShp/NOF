namespace NOF.Sample.Application.RequestHandlers;

public class RemoveConfigFile : IRequestHandler<RemoveConfigFileRequest>
{
    private readonly IConfigNodeRepository _configNodeRepository;
    private readonly IUnitOfWork _uow;

    public RemoveConfigFile(IConfigNodeRepository configNodeRepository, IUnitOfWork uow)
    {
        _configNodeRepository = configNodeRepository;
        _uow = uow;
    }

    public async Task<Result> HandleAsync(RemoveConfigFileRequest request, CancellationToken cancellationToken)
    {
        var id = ConfigNodeId.From(request.NodeId);
        var node = await _configNodeRepository.FindAsync(id, cancellationToken);

        if (node is null)
        {
            return Result.Fail(404, "Node not found.");
        }

        var fileName = ConfigFileName.From(request.FileName);
        node.RemoveConfigFile(fileName);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
