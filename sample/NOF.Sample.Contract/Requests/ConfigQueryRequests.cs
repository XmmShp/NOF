namespace NOF.Sample;

// 查询子节点
[ExposeToHttpEndpoint(HttpVerb.Get)]
public record GetConfigNodeChildrenRequest(long? ParentId) : IRequest<GetConfigNodeChildrenResponse>;

public record GetConfigNodeChildrenResponse(long NodeId, List<long> ChildrenIds);

// 查询单个节点
[ExposeToHttpEndpoint(HttpVerb.Get)]
public record GetConfigNodeByIdRequest(long Id) : IRequest<GetConfigNodeByIdResponse>;

public record GetConfigNodeByIdResponse(ConfigNodeDto Node);

// 查询根节点列表
[ExposeToHttpEndpoint(HttpVerb.Get)]
public record GetRootConfigNodesRequest : IRequest<GetRootConfigNodesResponse>;

public record GetRootConfigNodesResponse(List<ConfigNodeDto> Nodes);

public record ConfigNodeDto(
    long Id,
    long? ParentId,
    string Name,
    string? ActiveFileName,
    List<ConfigFileDto> ConfigFiles
);

public record ConfigFileDto(
    string Name,
    string Content
);