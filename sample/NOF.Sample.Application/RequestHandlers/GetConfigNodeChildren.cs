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
        if (!request.ParentId.HasValue)
        {
            return Result.Fail(400, "ParentId不能为空");
        }

        var nodeId = ConfigNodeId.From(request.ParentId.Value);
        var children = await _viewRepository.GetChildrenAsync(nodeId, cancellationToken);

        if (children is null)
        {
            return Result.Fail(404, "未找到子节点信息");
        }

        return new GetConfigNodeChildrenResponse(children.NodeId.Value, children.ChildrenIds);
    }
}
