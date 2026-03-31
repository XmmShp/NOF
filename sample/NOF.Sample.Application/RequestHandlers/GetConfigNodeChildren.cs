using NOF.Annotation;
using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.RequestHandlers;

[AutoInject(Lifetime.Scoped, RegisterTypes = new[] { typeof(NOFSampleService.GetConfigNodeChildren) })]
public class GetConfigNodeChildren : NOFSampleService.GetConfigNodeChildren
{
    private readonly IConfigNodeViewRepository _viewRepository;

    public GetConfigNodeChildren(IConfigNodeViewRepository viewRepository)
    {
        _viewRepository = viewRepository;
    }

    public async Task<Result<GetConfigNodeChildrenResponse>> GetConfigNodeChildrenAsync(GetConfigNodeChildrenRequest request, CancellationToken cancellationToken)
    {
        var nodeId = ConfigNodeId.Of(request.Id);
        var children = await _viewRepository.GetChildrenAsync(nodeId, cancellationToken);

        if (children is null)
        {
            return Result.Fail("404", "鏈壘鍒板瓙鑺傜偣淇℃伅");
        }

        return new GetConfigNodeChildrenResponse((long)children.NodeId, children.ChildrenIds);
    }
}





