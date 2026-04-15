using Microsoft.EntityFrameworkCore;
using NOF.Application;
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

    public async Task<Result<GetRootConfigNodesResponse>> GetRootConfigNodesAsync(GetRootConfigNodesRequest request)
    {
        var cancellationToken = CancellationToken.None;
        var nodes = await _dbContext.Set<ConfigNode>().GetRootNodesAsync(cancellationToken);

        var response = nodes.Select(node => _mapper.Map<ConfigNode, ConfigNodeDto>(node)).ToList();

        return new GetRootConfigNodesResponse
        {
            Nodes = response
        };
    }
}



