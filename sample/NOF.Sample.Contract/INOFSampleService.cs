using NOF.Contract;
using System.ComponentModel;

namespace NOF.Sample;

public partial interface INOFSampleService : IRpcService
{
    [AllowAnonymous]
    [HttpEndpoint(HttpVerb.Post, "api/config-nodes")]
    [Summary("创建配置节点")]
    [EndpointDescription("在指定父节点下创建一个新的配置节点，若不指定父节点则创建为根节点")]
    [Category("配置节点")]
    Task<Result> CreateConfigNodeAsync(CreateConfigNodeRequest request, CancellationToken cancellationToken = default);

    [AllowAnonymous]
    [HttpEndpoint(HttpVerb.Delete, "api/config-nodes/{id}")]
    [Summary("删除配置节点")]
    [EndpointDescription("根据节点 ID 删除指定的配置节点")]
    [Category("配置节点")]
    Task<Result> DeleteConfigNodeAsync(DeleteConfigNodeRequest request, CancellationToken cancellationToken = default);

    [AllowAnonymous]
    [HttpEndpoint(HttpVerb.Put, "api/config-nodes/{nodeId}/files/{fileName}")]
    [Summary("新增或更新配置文件")]
    [EndpointDescription("在指定节点下新增或更新一个配置文件的内容")]
    [Category("配置文件")]
    Task<Result> AddOrUpdateConfigFileAsync(AddOrUpdateConfigFileRequest request, CancellationToken cancellationToken = default);

    [AllowAnonymous]
    [HttpEndpoint(HttpVerb.Delete, "api/config-nodes/{nodeId}/files/{fileName}")]
    [Summary("删除配置文件")]
    [EndpointDescription("从指定节点中移除一个配置文件")]
    [Category("配置文件")]
    Task<Result> RemoveConfigFileAsync(RemoveConfigFileRequest request, CancellationToken cancellationToken = default);

    [AllowAnonymous]
    [HttpEndpoint(HttpVerb.Patch, "api/config-nodes/{nodeId}/active-file")]
    [Summary("设置活跃配置文件")]
    [EndpointDescription("设置指定节点的当前活跃配置文件，传入 null 可取消活跃文件")]
    [Category("配置文件")]
    Task<Result> SetActiveFileAsync(SetActiveFileRequest request, CancellationToken cancellationToken = default);

    [AllowAnonymous]
    [HttpEndpoint(HttpVerb.Patch, "api/config-nodes/{nodeId}/parent")]
    [Summary("更新节点父级")]
    [EndpointDescription("将指定配置节点移动到新的父节点下，传入 null 可将其设为根节点")]
    [Category("配置节点")]
    Task<Result> UpdateConfigNodeParentAsync(UpdateConfigNodeParentRequest request, CancellationToken cancellationToken = default);

    [AllowAnonymous]
    [HttpEndpoint(HttpVerb.Get, "api/config-nodes/{id}/children")]
    [Summary("查询指定节点的子节点列表")]
    [EndpointDescription("根据父节点 ID 获取其直接子节点的 ID 列表")]
    [Category("配置节点")]
    Task<Result<GetConfigNodeChildrenResponse>> GetConfigNodeChildrenAsync(GetConfigNodeChildrenRequest request, CancellationToken cancellationToken = default);

    [AllowAnonymous]
    [HttpEndpoint(HttpVerb.Get, "api/config-nodes/{id}")]
    [Summary("查询单个配置节点")]
    [EndpointDescription("根据节点 ID 获取配置节点的详细信息")]
    [Category("配置节点")]
    Task<Result<GetConfigNodeByIdResponse>> GetConfigNodeByIdAsync(GetConfigNodeByIdRequest request, CancellationToken cancellationToken = default);

    [AllowAnonymous]
    [HttpEndpoint(HttpVerb.Get, "api/config-nodes")]
    [Summary("查询所有根配置节点")]
    [EndpointDescription("获取所有没有父节点的顶层配置节点列表")]
    [Category("配置节点")]
    Task<Result<GetRootConfigNodesResponse>> GetRootConfigNodesAsync(GetRootConfigNodesRequest request, CancellationToken cancellationToken = default);

    Task<Result<GetConfigurationResponse>> GetConfigurationAsync(GetConfigurationRequest request, CancellationToken cancellationToken = default);

}
