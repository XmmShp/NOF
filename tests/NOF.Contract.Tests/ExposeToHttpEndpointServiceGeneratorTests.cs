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
        typeof(GenerateServiceAttribute),
        typeof(HttpVerb),
        typeof(Result),
        typeof(Result<>)
    ];

    [Fact]
    public void GeneratesHttpClient_FromDeclaredServiceMethods()
    {
        const string source = """
                              using NOF.Contract;
                              using System.Threading;
                              using System.Threading.Tasks;

                              namespace MyApp
                              {
                                  public record CreateUserRequest(string Name);

                                  [GenerateService]
                                  public partial interface IMyService
                                  {
                                      [HttpEndpoint(HttpVerb.Post, "/api/users")]
                                      Task<Result> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
                                  }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        runResult.GeneratedTrees.Should().HaveCount(1);

        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();
        code.Should().Contain("public partial class HttpMyService : IMyService");
        code.Should().Contain("CreateUserAsync");
        code.Should().Contain("HttpMethod.Post");
        code.Should().Contain("JsonContent.Create(request, typeof(MyApp.CreateUserRequest), options: _jsonOptions);");
        code.Should().NotContain("HttpCompletionOption");
        code.Should().NotContain("IRequestSender");
    }

    [Fact]
    public void GenerateHttpClientFalse_GeneratesNoClientImplementation()
    {
        const string source = """
                              using NOF.Contract;
                              using System.Threading.Tasks;

                              namespace MyApp
                              {
                                  public record CreateItemRequest(string Name);

                                  [GenerateService(GenerateHttpClient = false)]
                                  public partial interface IMyService
                                  {
                                      [HttpEndpoint(HttpVerb.Post, "/api/items")]
                                      Task<Result> CreateItemAsync(CreateItemRequest request);
                                  }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        runResult.GeneratedTrees.Should().HaveCount(1);
        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();
        code.Should().NotContain("class HttpMyService");
    }

    [Fact]
    public void MethodWithoutHttpEndpoint_DefaultsToPostAndOperationRoute()
    {
        const string source = """
                              using NOF.Contract;
                              using System.Threading.Tasks;

                              namespace MyApp
                              {
                                  public record InternalRequest(string Data);

                                  [GenerateService]
                                  public partial interface IMyService
                                  {
                                      Task<Result> InternalAsync(InternalRequest request);
                                  }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();
        code.Should().Contain("HttpMethod.Post");
        code.Should().Contain("var endpoint = \"Internal\";");
    }

    [Fact]
    public void GetRouteParams_AreExcludedFromQueryAndBody()
    {
        const string source = """
                              using NOF.Contract;
                              using System.Threading.Tasks;

                              namespace MyApp
                              {
                                  public class GetUserPostsRequest
                                  {
                                      public int Id { get; set; }
                                      public int Page { get; set; }
                                      public int PageSize { get; set; }
                                  }

                                  [GenerateService]
                                  public partial interface IMyService
                                  {
                                      [HttpEndpoint(HttpVerb.Get, "/api/users/{id}/posts")]
                                      Task<Result> GetUserPostsAsync(GetUserPostsRequest request);
                                  }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();
        code.Should().Contain("global::System.Uri.EscapeDataString(request.Id.ToString()!)");
        code.Should().Contain("queryParts");
        code.Should().Contain("Page");
        code.Should().Contain("PageSize");
    }

    [Fact]
    public void PutRouteParams_UseBodyDictionaryWithoutRouteKeys()
    {
        const string source = """
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

                                  [GenerateService]
                                  public partial interface IMyService
                                  {
                                      [HttpEndpoint(HttpVerb.Put, "/api/users/{id}")]
                                      Task<Result> UpdateUserAsync(UpdateUserRequest request);
                                  }
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

    [Fact]
    public void TaskWithoutValue_ReturnsAfterEnsureSuccess()
    {
        const string source = """
                              using NOF.Contract;
                              using System.Threading.Tasks;

                              namespace MyApp
                              {
                                  public record PingRequest;

                                  [GenerateService]
                                  public partial interface IMyService
                                  {
                                      Task PingAsync(PingRequest request);
                                  }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();
        code.Should().Contain("response.EnsureSuccessStatusCode();");
        code.Should().Contain("return;");
    }
}
