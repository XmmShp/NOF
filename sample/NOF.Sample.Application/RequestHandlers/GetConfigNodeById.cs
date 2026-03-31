using NOF.Annotation;
using NOF.Contract;
using NOF.Sample.Application.Repositories;

namespace NOF.Sample.Application.RequestHandlers;

[AutoInject(Lifetime.Scoped, RegisterTypes = [typeof(NOFSampleService.GetConfigNodeById)])]
public class GetConfigNodeById : NOFSampleService.GetConfigNodeById
{
    private readonly IConfigNodeViewRepository _viewRepository;

    public GetConfigNodeById(IConfigNodeViewRepository viewRepository)
    {
        _viewRepository = viewRepository;
    }

    public async Task<Result<GetConfigNodeByIdResponse>> GetConfigNodeByIdAsync(GetConfigNodeByIdRequest request, CancellationToken cancellationToken)
    {
        var nodeId = ConfigNodeId.Of(request.Id);
        var node = await _viewRepository.GetByIdAsync(nodeId, cancellationToken);

        if (node is null)
        {
            return Result.Fail("404", "Config node not found.");
        }

        var dto = node with { ConfigFiles = node.ConfigFiles.Select(f => new ConfigFileDto(f.Name, f.Content)).ToList() };

        return new GetConfigNodeByIdResponse(dto);
    }
}





