using Microsoft.EntityFrameworkCore;
using NOF.Contract;
using NOF.Sample.Application.Entities;

namespace NOF.Sample.Application.RequestHandlers;

public class GetConfigNodeChildren : NOFSampleService.GetConfigNodeChildren
{
    private readonly DbContext _dbContext;

    public GetConfigNodeChildren(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task<Result<GetConfigNodeChildrenResponse>> HandleAsync(GetConfigNodeChildrenRequest request, CancellationToken cancellationToken)
    {
        var nodeId = ConfigNodeId.Of(request.Id);
        var children = await _dbContext.Set<ConfigNodeChildren>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.NodeId == nodeId, cancellationToken);

        if (children is null)
        {
            return Result.Fail("404", "未找到子节点信息");
        }

        return new GetConfigNodeChildrenResponse
        {
            NodeId = (long)children.NodeId,
            ChildrenIds = children.ChildrenIds
        };
    }
}



