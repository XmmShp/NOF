using NOF.Contract;
using System.ComponentModel;

namespace NOF.Sample;

public interface IDemoDownstreamService : IRpcService
{
    [HttpEndpoint(HttpVerb.Post, "rpc/OAuthChainDemo/Downstream/Inspect")]
    [Summary("消费 access token")]
    [Description("由下游服务读取 access token 中的 subject、permissions、scopes 与 proxy service name")]
    [Category("认证演示")]
    Result<ConsumeDemoAccessTokenResponse> InspectAccessToken(Empty request);
}
