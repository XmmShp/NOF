using System.ComponentModel;

namespace NOF.Sample;

// 查询子节点
[AllowAnonymous]
[Summary("查询指定节点的子节点列表")]
[Description("根据父节点 ID 获取其直接子节点的 ID 列表")]
[DisplayName("获取子节点")]
[Category("配置节点")]
[ExposeToHttpEndpoint(HttpVerb.Get, "api/config-nodes/{id}/children")]
public record GetConfigNodeChildrenRequest(long Id) : IRequest<GetConfigNodeChildrenResponse>;

public record GetConfigNodeChildrenResponse(long NodeId, List<long> ChildrenIds);

// 查询单个节点
[AllowAnonymous]
[Summary("查询单个配置节点")]
[Description("根据节点 ID 获取配置节点的详细信息")]
[DisplayName("获取配置节点")]
[Category("配置节点")]
[ExposeToHttpEndpoint(HttpVerb.Get, "api/config-nodes/{id}")]
public record GetConfigNodeByIdRequest(long Id) : IRequest<GetConfigNodeByIdResponse>;

public record GetConfigNodeByIdResponse(ConfigNodeDto Node);

// 查询根节点列表
[AllowAnonymous]
[Summary("查询所有根配置节点")]
[Description("获取所有没有父节点的顶层配置节点列表")]
[DisplayName("获取根节点列表")]
[Category("配置节点")]
[ExposeToHttpEndpoint(HttpVerb.Get, "api/config-nodes")]
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