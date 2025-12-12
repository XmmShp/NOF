namespace NOF.Sample.Application.RequestHandlers;

public class SetActiveFile : IRequestHandler<SetActiveFileRequest>
{
    private readonly IConfigNodeRepository _configNodeRepository;
    private readonly IUnitOfWork _uow;

    public SetActiveFile(IConfigNodeRepository configNodeRepository, IUnitOfWork uow)
    {
        _configNodeRepository = configNodeRepository;
        _uow = uow;
    }

    public async Task<Result> HandleAsync(SetActiveFileRequest request, CancellationToken cancellationToken)
    {
        var id = ConfigNodeId.From(request.NodeId);
        var node = await _configNodeRepository.FindAsync(id, cancellationToken);

        if (node is null)
        {
            return Result.Fail(404, "Node not found.");
        }

        var fileName = string.IsNullOrEmpty(request.FileName) ? (ConfigFileName?)null : ConfigFileName.From(request.FileName);
        node.SetActiveFileName(fileName);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
