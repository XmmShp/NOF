using System.ComponentModel;

namespace NOF.Sample;

[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Post, "api/config-nodes")]
[Summary("创建配置节点")]
[EndpointDescription("在指定父节点下创建一个新的配置节点，若不指定父节点则创建为根节点")]
[Category("配置节点")]
public record CreateConfigNodeRequest(string Name, long? ParentId) : IRequest;

[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Delete, "api/config-nodes/{id}")]
[Summary("删除配置节点")]
[EndpointDescription("根据节点 ID 删除指定的配置节点")]
[Category("配置节点")]
public record DeleteConfigNodeRequest(long Id) : IRequest;

[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Put, "api/config-nodes/{nodeId}/files/{fileName}")]
[Summary("新增或更新配置文件")]
[EndpointDescription("在指定节点下新增或更新一个配置文件的内容")]
[Category("配置文件")]
public record AddOrUpdateConfigFileRequest(long NodeId, string FileName, string Content) : IRequest;

[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Delete, "api/config-nodes/{nodeId}/files/{fileName}")]
[Summary("删除配置文件")]
[EndpointDescription("从指定节点中移除一个配置文件")]
[Category("配置文件")]
public record RemoveConfigFileRequest(long NodeId, string FileName) : IRequest;

[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Patch, "api/config-nodes/{nodeId}/active-file")]
[Summary("设置活跃配置文件")]
[EndpointDescription("设置指定节点的当前活跃配置文件，传入 null 可取消活跃文件")]
[Category("配置文件")]
public record SetActiveFileRequest(long NodeId, string? FileName) : IRequest;
