using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.RequestHandlers;

public class GetConfigNodeById : IRequestHandler<GetConfigNodeByIdRequest, GetConfigNodeByIdResponse>
{
    private readonly IConfigNodeViewRepository _viewRepository;

    public GetConfigNodeById(IConfigNodeViewRepository viewRepository)
    {
        _viewRepository = viewRepository;
    }

    public async Task<Result<GetConfigNodeByIdResponse>> HandleAsync(GetConfigNodeByIdRequest request, CancellationToken cancellationToken)
    {
        var nodeId = ConfigNodeId.From(request.Id);
        var node = await _viewRepository.GetByIdAsync(nodeId, cancellationToken);

        if (node is null)
        {
            return Result.Fail(404, "配置节点不存在");
        }

        var dto = node with { ConfigFiles = node.ConfigFiles.Select(f => new ConfigFileDto(f.Name, f.Content)).ToList() };

        return new GetConfigNodeByIdResponse(dto);
    }
}
