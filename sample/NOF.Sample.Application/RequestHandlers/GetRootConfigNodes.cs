using NOF.Annotation;
using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.RequestHandlers;

[AutoInject(Lifetime.Scoped, RegisterTypes = new[] { typeof(NOFSampleService.GetRootConfigNodes) })]
public class GetRootConfigNodes : NOFSampleService.GetRootConfigNodes
{
    private readonly IConfigNodeViewRepository _viewRepository;

    public GetRootConfigNodes(IConfigNodeViewRepository viewRepository)
    {
        _viewRepository = viewRepository;
    }

    public async Task<Result<GetRootConfigNodesResponse>> GetRootConfigNodesAsync(GetRootConfigNodesRequest request, CancellationToken cancellationToken)
    {
        var nodes = await _viewRepository.GetRootNodesAsync(cancellationToken);

        var response = nodes.Select(node => new ConfigNodeDto(
            node.Id,
            node.ParentId,
            node.Name,
            node.ActiveFileName,
            node.ConfigFiles.Select(f => new ConfigFileDto(f.Name, f.Content)).ToList()
        )).ToList();

        return new GetRootConfigNodesResponse(response);
    }
}





