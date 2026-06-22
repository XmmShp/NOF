using NOF.Contract;
using System.ComponentModel;

namespace NOF.Sample;

public interface IOAuthChainDemoService : IRpcService
{
    [HttpEndpoint(HttpVerb.Post, "rpc/OAuthChainDemo/CreateClient")]
    [Summary("创建 OAuth 演示客户端")]
    [Description("创建一个可用于 client_credentials 和 token exchange 演示的 OAuth client，并返回一次性的 client secret")]
    [Category("认证演示")]
    Result<CreateDemoOAuthClientResponse> CreateClient(CreateDemoOAuthClientRequest request);

    [HttpEndpoint(HttpVerb.Post, "rpc/OAuthChainDemo/GetClientToken")]
    [Summary("获取服务访问令牌")]
    [Description("用 client credentials 为演示客户端申请 access token")]
    [Category("认证演示")]
    Result<DemoTokenResponse> GetClientToken(GetDemoClientTokenRequest request);

    [HttpEndpoint(HttpVerb.Post, "rpc/OAuthChainDemo/GetUserToken")]
    [Summary("获取用户访问令牌")]
    [Description("在服务端模拟 authorization_code + PKCE，为 demo-user 获取 access token")]
    [Category("认证演示")]
    Result<DemoTokenResponse> GetUserToken(GetDemoUserTokenRequest request);

    [HttpEndpoint(HttpVerb.Post, "rpc/OAuthChainDemo/ExchangeToken")]
    [Summary("交换用户令牌")]
    [Description("将用户 token 作为 subject_token、服务 token 作为 actor_token 发起 token exchange")]
    [Category("认证演示")]
    Result<DemoTokenResponse> ExchangeToken(ExchangeDemoTokenRequest request);

    [HttpEndpoint(HttpVerb.Post, "rpc/OAuthChainDemo/CallDownstream")]
    [Summary("调用下游服务")]
    [Description("在后端通过 HTTPRpcClient self-call 下游服务，并消费交换后的 access token")]
    [Category("认证演示")]
    Result<ConsumeDemoAccessTokenResponse> CallDownstream(CallDemoDownstreamRequest request);
}
