using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.RequestHandlers;

public class GetRootConfigNodes : IRequestHandler<GetRootConfigNodesRequest, GetRootConfigNodesResponse>
{
    private readonly IConfigNodeViewRepository _viewRepository;

    public GetRootConfigNodes(IConfigNodeViewRepository viewRepository)
    {
        _viewRepository = viewRepository;
    }

    public async Task<Result<GetRootConfigNodesResponse>> HandleAsync(GetRootConfigNodesRequest request, CancellationToken cancellationToken)
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
