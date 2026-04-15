using NOF.Contract;
using System.ComponentModel;

namespace NOF.Sample;

public interface INOFSampleService : IRpcService
{
    [HttpEndpoint(HttpVerb.Post, "api/config-nodes")]
    [Summary("创建配置节点")]
    [Description("在指定父节点下创建一个新的配置节点，若不指定父节点则创建为根节点")]
    [Category("配置节点")]
    Result CreateConfigNode(CreateConfigNodeRequest request);

    [HttpEndpoint(HttpVerb.Delete, "api/config-nodes/{id}")]
    [Summary("删除配置节点")]
    [Description("根据节点 ID 删除指定的配置节点")]
    [Category("配置节点")]
    Result DeleteConfigNode(DeleteConfigNodeRequest request);

    [HttpEndpoint(HttpVerb.Put, "api/config-nodes/{nodeId}/files/{fileName}")]
    [Summary("新增或更新配置文件")]
    [Description("在指定节点下新增或更新一个配置文件的内容")]
    [Category("配置文件")]
    Result AddOrUpdateConfigFile(AddOrUpdateConfigFileRequest request);

    [HttpEndpoint(HttpVerb.Delete, "api/config-nodes/{nodeId}/files/{fileName}")]
    [Summary("删除配置文件")]
    [Description("从指定节点中移除一个配置文件")]
    [Category("配置文件")]
    Result RemoveConfigFile(RemoveConfigFileRequest request);

    [HttpEndpoint(HttpVerb.Patch, "api/config-nodes/{nodeId}/active-file")]
    [Summary("设置活跃配置文件")]
    [Description("设置指定节点的当前活跃配置文件，传入 null 可取消活跃文件")]
    [Category("配置文件")]
    Result SetActiveFile(SetActiveFileRequest request);

    [HttpEndpoint(HttpVerb.Patch, "api/config-nodes/{nodeId}/parent")]
    [Summary("更新节点父级")]
    [Description("将指定配置节点移动到新的父节点下，传入 null 可将其设为根节点")]
    [Category("配置节点")]
    Result UpdateConfigNodeParent(UpdateConfigNodeParentRequest request);

    [HttpEndpoint(HttpVerb.Get, "api/config-nodes/{id}/children")]
    [Summary("查询指定节点的子节点列表")]
    [Description("根据父节点 ID 获取其直接子节点的 ID 列表")]
    [Category("配置节点")]
    Result<GetConfigNodeChildrenResponse> GetConfigNodeChildren(GetConfigNodeChildrenRequest request);

    [HttpEndpoint(HttpVerb.Get, "api/config-nodes/{id}")]
    [Summary("查询单个配置节点")]
    [Description("根据节点 ID 获取配置节点的详细信息")]
    [Category("配置节点")]
    Result<GetConfigNodeByIdResponse> GetConfigNodeById(GetConfigNodeByIdRequest request);

    [HttpEndpoint(HttpVerb.Get, "api/config-nodes")]
    [Summary("查询所有根配置节点")]
    [Description("获取所有没有父节点的顶层配置节点列表")]
    [Category("配置节点")]
    Result<GetRootConfigNodesResponse> GetRootConfigNodes(GetRootConfigNodesRequest request);

    Result<GetConfigurationResponse> GetConfiguration(GetConfigurationRequest request);

}
