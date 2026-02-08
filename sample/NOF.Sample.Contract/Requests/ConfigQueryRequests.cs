using System.ComponentModel;

namespace NOF.Sample;

// 查询子节点
[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Get, "api/config-nodes/{id}/children")]
[Summary("查询指定节点的子节点列表")]
[EndpointDescription("根据父节点 ID 获取其直接子节点的 ID 列表")]
[Category("配置节点")]
public record GetConfigNodeChildrenRequest(long Id) : IRequest<GetConfigNodeChildrenResponse>;

public record GetConfigNodeChildrenResponse(long NodeId, List<long> ChildrenIds);

// 查询单个节点
[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Get, "api/config-nodes/{id}")]
[Summary("查询单个配置节点")]
[EndpointDescription("根据节点 ID 获取配置节点的详细信息")]
[Category("配置节点")]
public record GetConfigNodeByIdRequest(long Id) : IRequest<GetConfigNodeByIdResponse>;

public record GetConfigNodeByIdResponse(ConfigNodeDto Node);

// 查询根节点列表
[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Get, "api/config-nodes")]
[Summary("查询所有根配置节点")]
[EndpointDescription("获取所有没有父节点的顶层配置节点列表")]
[Category("配置节点")]
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
