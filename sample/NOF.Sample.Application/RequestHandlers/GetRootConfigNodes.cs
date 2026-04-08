using NOF.Application;
using NOF.Contract;
using NOF.Domain;

namespace NOF.Sample.Application.RequestHandlers;

public class GetRootConfigNodes : NOFSampleService.GetRootConfigNodes
{
    private readonly IRepository<ConfigNode, ConfigNodeId> _configNodeRepository;
    private readonly IMapper _mapper;

    public GetRootConfigNodes(IRepository<ConfigNode, ConfigNodeId> configNodeRepository, IMapper mapper)
    {
        _configNodeRepository = configNodeRepository;
        _mapper = mapper;
    }

    public async Task<Result<GetRootConfigNodesResponse>> GetRootConfigNodesAsync(GetRootConfigNodesRequest request, CancellationToken cancellationToken)
    {
        var nodes = await _configNodeRepository.GetRootNodesAsync(cancellationToken);

        var response = nodes.Select(node => _mapper.Map<ConfigNode, ConfigNodeDto>(node)).ToList();

        return new GetRootConfigNodesResponse(response);
    }
}





