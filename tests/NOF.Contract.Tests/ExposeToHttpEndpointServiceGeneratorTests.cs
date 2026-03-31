using FluentAssertions;
using NOF.Contract;
using NOF.Contract.SourceGenerator;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class ExposeToHttpEndpointServiceGeneratorTests
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
                              using NOF.Contract;
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

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        runResult.GeneratedTrees.Should().HaveCount(1);

        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();
        code.Should().Contain("public partial class MyServiceClient : global::MyApp.IMyService");
        code.Should().Contain("CreateUserAsync");
        code.Should().Contain("HttpMethod.Post");
    }

    [Fact]
    public void DoesNotGenerate_WhenServiceIsNotRpcService()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Contract;
                              using System.Threading.Tasks;

                              namespace MyApp
                              {
                                  public record PingRequest;

                                  public partial interface IMyService
                                  {
                                      Task<Result> PingAsync(PingRequest request);
                                  }

                                  [HttpServiceClient<IMyService>]
                                  public partial class MyServiceClient;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        runResult.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void MethodWithoutHttpEndpoint_DefaultsToPostAndOperationRoute()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Contract;
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

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();
        code.Should().Contain("HttpMethod.Post");
        code.Should().Contain("var endpoint = \"Internal\";");
    }

    [Fact]
    public void PutRouteParams_UseBodyDictionaryWithoutRouteKeys()
    {
        const string source = """
                              using NOF.Contract;
                              using NOF.Contract;
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

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        code.Should().Contain("global::System.Uri.EscapeDataString(request.Id.ToString()!)");
        code.Should().Contain("Dictionary<string, object?>");
        code.Should().Contain("body[\"Name\"] = request.Name");
        code.Should().Contain("body[\"Email\"] = request.Email");
        code.Should().NotContain("body[\"Id\"]");
    }
}

