using System.ComponentModel;

namespace NOF.Sample;

[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Patch, "api/config-nodes/{nodeId}/parent")]
[Summary("更新节点父级")]
[EndpointDescription("将指定配置节点移动到新的父节点下，传入 null 可将其设为根节点")]
[Category("配置节点")]
public record UpdateConfigNodeParentRequest(long NodeId, long? NewParentId) : IRequest;
