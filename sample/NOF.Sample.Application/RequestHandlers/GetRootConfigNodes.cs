using Microsoft.EntityFrameworkCore;
using NOF.Application;
using NOF.Abstraction;
using NOF.Contract;

namespace NOF.Sample.Application.RequestHandlers;

public class GetRootConfigNodes : NOFSampleService.GetRootConfigNodes
{
    private readonly DbContext _dbContext;
    private readonly IMapper _mapper;

    public GetRootConfigNodes(DbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public override async Task<RpcResult<Result<GetRootConfigNodesResponse>>> HandleAsync(GetRootConfigNodesRequest request, NOFContext context, CancellationToken cancellationToken)
    {
        var nodes = await _dbContext.Set<ConfigNode>().GetRootNodesAsync(cancellationToken);

        var response = nodes.Select(node => _mapper.Map<ConfigNode, ConfigNodeDto>(node)).ToList();

        return Success((Result<GetRootConfigNodesResponse>)new GetRootConfigNodesResponse
        {
            Nodes = response
        });
    }
}


