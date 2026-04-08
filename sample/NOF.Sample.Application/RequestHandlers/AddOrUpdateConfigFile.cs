using NOF.Application;
using NOF.Contract;
using NOF.Domain;

namespace NOF.Sample.Application.RequestHandlers;

public class AddOrUpdateConfigFile : NOFSampleService.AddOrUpdateConfigFile
{
    private readonly IRepository<ConfigNode, ConfigNodeId> _configNodeRepository;
    private readonly IUnitOfWork _uow;

    public AddOrUpdateConfigFile(IRepository<ConfigNode, ConfigNodeId> configNodeRepository, IUnitOfWork uow)
    {
        _configNodeRepository = configNodeRepository;
        _uow = uow;
    }

    public async Task<Result> AddOrUpdateConfigFileAsync(AddOrUpdateConfigFileRequest request, CancellationToken cancellationToken)
    {
        var id = ConfigNodeId.Of(request.NodeId);
        var node = await _configNodeRepository.FindAsync(id, cancellationToken);

        if (node is null)
        {
            return Result.Fail("404", "Node not found.");
        }

        var fileName = ConfigFileName.Of(request.FileName);
        var content = ConfigContent.Of(request.Content);

        node.AddOrUpdateConfigFile(fileName, content);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}




