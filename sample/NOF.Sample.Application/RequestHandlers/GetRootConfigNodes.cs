using NOF.Application;
using NOF.Contract;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.RequestHandlers;

public class GetRootConfigNodes : NOFSampleService.GetRootConfigNodes
{
    private readonly IDbContext _dbContext;

    public GetRootConfigNodes(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task<Result<GetRootConfigNodesResponse>> HandleAsync(GetRootConfigNodesRequest request, Context context, CancellationToken cancellationToken)
    {
        var response = await _dbContext.Set<ConfigNode>()
            .QueryRootNodes()
            .ProjectTo<ConfigNodeDto>()
            .ToListAsync(cancellationToken);

        return new GetRootConfigNodesResponse
        {
            Nodes = response
        };
    }
}
