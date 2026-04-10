using NOF.Contract;
using NOF.Hosting;
using NOF.Hosting.SourceGenerator;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class RpcServiceClientGeneratorTests
{
    private static readonly Type[] _extraRefs =
    [
        typeof(HttpEndpointAttribute),
        typeof(HttpServiceClientAttribute<>),
        typeof(IRpcService),
        typeof(HttpVerb),
        typeof(Result),
        typeof(Result<>)
    ];

    [Fact]
    public void GeneratesHttpClient_OnAttributedPartialClass()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Hosting;
                              using System.Threading;
                              using System.Threading.Tasks;

                              namespace MyApp
                              {
                                  public record CreateUserRequest(string Name);

                                  public partial interface IMyService : IRpcService
                                  {
                                      [HttpEndpoint(HttpVerb.Post, "/api/users")]
                                      Task<Result> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
                                  }

                                  [HttpServiceClient<IMyService>]
                                  public partial class MyServiceClient;
                              }
                              """;

        var runResult = new RpcServiceClientGenerator().GetResult(source, _extraRefs);
        Assert.Single(runResult.GeneratedTrees);

        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();
        Assert.Contains("public partial class MyServiceClient : global::MyApp.IMyService", code);
        Assert.Contains("CreateUserAsync", code);
        Assert.Contains("HttpMethod.Post", code);
    }

    [Fact]
    public void MethodWithoutHttpEndpoint_DefaultsToPostAndOperationRoute()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Hosting;
                              using System.Threading.Tasks;

                              namespace MyApp
                              {
                                  public record InternalRequest(string Data);

                                  public partial interface IMyService : IRpcService
                                  {
                                      Task<Result> InternalAsync(InternalRequest request);
                                  }

                                  [HttpServiceClient<IMyService>]
                                  public partial class MyServiceClient;
                              }
                              """;

        var runResult = new RpcServiceClientGenerator().GetResult(source, _extraRefs);
        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();
        Assert.Contains("HttpMethod.Post", code);
        Assert.Contains("var endpoint = \"Internal\";", code);
    }

    [Fact]
    public void PutRouteParams_UseBodyDictionaryWithoutRouteKeys()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Hosting;
                              using System.Threading.Tasks;

                              namespace MyApp
                              {
                                  public class UpdateUserRequest
                                  {
                                      public int Id { get; set; }
                                      public string Name { get; set; } = default!;
                                      public string Email { get; set; } = default!;
                                  }

                                  public partial interface IMyService : IRpcService
                                  {
                                      [HttpEndpoint(HttpVerb.Put, "/api/users/{id}")]
                                      Task<Result> UpdateUserAsync(UpdateUserRequest request);
                                  }

                                  [HttpServiceClient<IMyService>]
                                  public partial class MyServiceClient;
                              }
                              """;

        var runResult = new RpcServiceClientGenerator().GetResult(source, _extraRefs);
        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        Assert.Contains("global::System.Uri.EscapeDataString(request.Id.ToString()!)", code);
        Assert.Contains("Dictionary<string, object?>", code);
        Assert.Contains("body[\"Name\"] = request.Name", code);
        Assert.Contains("body[\"Email\"] = request.Email", code);
        Assert.DoesNotContain("body[\"Id\"]", code);
    }

    [Fact]
    public void MethodWithoutRequestParam_ShouldNotGenerateMessageVariable()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Hosting;
                              using System.Threading;
                              using System.Threading.Tasks;

                              namespace MyApp
                              {
                                  public record MyData(string Value);

                                  public partial interface IMyService : IRpcService
                                  {
                                      [HttpEndpoint(HttpVerb.Get, "/api/data")]
                                      Task<Result<MyData>> GetDataAsync(CancellationToken cancellationToken = default);
                                  }

                                  [HttpServiceClient<IMyService>]
                                  public partial class MyServiceClient;
                              }
                              """;

        var runResult = new RpcServiceClientGenerator().GetResult(source, _extraRefs);
        Assert.Single(runResult.GeneratedTrees);

        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();
        Assert.DoesNotContain("var message = null", code);
        Assert.Contains("Message = null,", code);
        Assert.Contains("GetDataAsync", code);
    }
}


