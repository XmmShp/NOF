using System.ComponentModel;

namespace NOF.Sample;

[AllowAnonymous]
[Summary("创建配置节点")]
[Description("在指定父节点下创建一个新的配置节点，若不指定父节点则创建为根节点")]
[DisplayName("创建配置节点")]
[Category("配置节点")]
[ExposeToHttpEndpoint(HttpVerb.Post, "api/config-nodes")]
public record CreateConfigNodeRequest(string Name, long? ParentId) : IRequest;

[AllowAnonymous]
[Summary("删除配置节点")]
[Description("根据节点 ID 删除指定的配置节点")]
[DisplayName("删除配置节点")]
[Category("配置节点")]
[ExposeToHttpEndpoint(HttpVerb.Delete, "api/config-nodes/{id}")]
public record DeleteConfigNodeRequest(long Id) : IRequest;

[AllowAnonymous]
[Summary("新增或更新配置文件")]
[Description("在指定节点下新增或更新一个配置文件的内容")]
[DisplayName("新增或更新配置文件")]
[Category("配置文件")]
[ExposeToHttpEndpoint(HttpVerb.Put, "api/config-nodes/{nodeId}/files/{fileName}")]
public record AddOrUpdateConfigFileRequest(long NodeId, string FileName, string Content) : IRequest;

[AllowAnonymous]
[Summary("删除配置文件")]
[Description("从指定节点中移除一个配置文件")]
[DisplayName("删除配置文件")]
[Category("配置文件")]
[ExposeToHttpEndpoint(HttpVerb.Delete, "api/config-nodes/{nodeId}/files/{fileName}")]
public record RemoveConfigFileRequest(long NodeId, string FileName) : IRequest;

[AllowAnonymous]
[Summary("设置活跃配置文件")]
[Description("设置指定节点的当前活跃配置文件，传入 null 可取消活跃文件")]
[DisplayName("设置活跃文件")]
[Category("配置文件")]
[ExposeToHttpEndpoint(HttpVerb.Patch, "api/config-nodes/{nodeId}/active-file")]
public record SetActiveFileRequest(long NodeId, string? FileName) : IRequest;
