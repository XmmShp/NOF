using NOF.Contract;
using NOF.Sample.Application.Entities;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.RequestHandlers;

public class GetConfigNodeChildren : NOFSampleService.GetConfigNodeChildren
{
    private readonly IConfigNodeChildrenRepository _childrenRepository;

    public GetConfigNodeChildren(IConfigNodeChildrenRepository childrenRepository)
    {
        _childrenRepository = childrenRepository;
    }

    public async Task<Result<GetConfigNodeChildrenResponse>> GetConfigNodeChildrenAsync(GetConfigNodeChildrenRequest request, CancellationToken cancellationToken)
    {
        var nodeId = ConfigNodeId.Of(request.Id);
        var children = await _childrenRepository.GetChildrenAsync(nodeId, cancellationToken);

        if (children is null)
        {
            return Result.Fail("404", "未找到子节点信息");
        }

        return new GetConfigNodeChildrenResponse((long)children.NodeId, children.ChildrenIds);
    }
}





