using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.RequestHandlers;

public class GetConfigNodeChildren : IRequestHandler<GetConfigNodeChildrenRequest, GetConfigNodeChildrenResponse>
{
    private readonly IConfigNodeViewRepository _viewRepository;

    public GetConfigNodeChildren(IConfigNodeViewRepository viewRepository)
    {
        _viewRepository = viewRepository;
    }

    public async Task<Result<GetConfigNodeChildrenResponse>> HandleAsync(GetConfigNodeChildrenRequest request, CancellationToken cancellationToken)
    {
        var nodeId = ConfigNodeId.From(request.Id);
        var children = await _viewRepository.GetChildrenAsync(nodeId, cancellationToken);

        if (children is null)
        {
            return Result.Fail(404, "未找到子节点信息");
        }

        return new GetConfigNodeChildrenResponse(children.NodeId.Value, children.ChildrenIds);
    }
}
