using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NOF.Contract;
using NOF.Contract.SourceGenerator;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class ExposeToHttpEndpointServiceGeneratorTests
{
    private static readonly Type[] _extraRefs =
    [        typeof(HttpEndpointAttribute),
        typeof(GenerateServiceAttribute),
        typeof(HttpVerb),        typeof(Result),
        typeof(Result<>)
    ];

    [Fact]
    public void GeneratesInterfaceAndBothImplementations()
    {
        const string source = """
                              using NOF.Contract;

                              namespace MyApp
                              {
                                                                    [HttpEndpoint(HttpVerb.Post, "/api/users")]
                                  public partial class CreateUserRequest
                                  {
                                      public string Name { get; set; } = default!;
                                  }

                                  [GenerateService]
                                  public partial interface IMyService;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        runResult.GeneratedTrees.Should().HaveCount(1);

        var root = runResult.GeneratedTrees[0].GetRoot();
        var code = root.ToFullString();

        // Interface
        root.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
            .Should().Contain(i => i.Identifier.Text == "IMyService");

        // HTTP client: Http + MyService
        root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Should().Contain(c => c.Identifier.Text == "HttpMyService");

        // Method name: CreateUser (from CreateUserRequest minus "Request")
        code.Should().Contain("CreateUserAsync");

        // Methods have 2 params (request + cancellationToken), no HttpCompletionOption
        root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Should().Contain(m =>
                m.Identifier.Text == "CreateUserAsync"
                && m.ParameterList.Parameters.Count == 2
                && m.ParameterList.Parameters[0].Type!.ToString() == "MyApp.CreateUserRequest");

        // No HttpCompletionOption
        code.Should().NotContain("HttpCompletionOption");
    }

    [Fact]
    public void GeneratesCustomOperationName()
    {
        const string source = """
                              using NOF.Contract;

                              namespace MyApp
                              {
                                                                    [HttpEndpoint(HttpVerb.Get, "/users/profile")]
                                  public partial class UserProfileRequest { }

                                  [GenerateService]
                                  public partial interface IMyService;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        runResult.GeneratedTrees.Should().HaveCount(1);

        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();
        code.Should().Contain("GetProfileAsync");
    }

    [Fact]
    public void GeneratesVirtualMethods()
    {
        const string source = """
                              using NOF.Contract;

                              namespace MyApp
                              {
                                                                    [HttpEndpoint(HttpVerb.Post, "/api/items")]
                                  public record CreateItemRequest(string Name);

                                  [GenerateService]
                                  public partial interface IMyService;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        // HTTP methods should be virtual
        code.Should().Contain("public virtual async");
    }

    [Fact]
    public void GeneratesPartialClasses()
    {
        const string source = """
                              using NOF.Contract;

                              namespace MyApp
                              {
                                                                    [HttpEndpoint(HttpVerb.Delete, "/api/items/{id}")]
                                  public record DeleteItemRequest(long Id);

                                  [GenerateService]
                                  public partial interface IMyService;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        var root = runResult.GeneratedTrees[0].GetRoot();

        // Both implementation classes should be partial
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
        classes.Should().OnlyContain(c => c.Modifiers.Any(m => m.Text == "partial"));
    }

    [Fact]
    public void DoesNotGenerateWhenNoPublicApiRequests()
    {
        const string source = """
                              using NOF.Contract;

                              namespace MyApp
                              {
                                  // No [PublicApi] â€?should be ignored
                                  [HttpEndpoint(HttpVerb.Post)]
                                  public class PlainRequest { }

                                  [GenerateService]
                                  public partial interface IMyService;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        runResult.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void ScansOnlyInterfaceNamespace()
    {
        const string source = """
                              using NOF.Contract;

                              namespace Other
                              {
                                                                    [HttpEndpoint(HttpVerb.Get, "/api/other")]
                                  public record OtherRequest;
                              }

                              namespace MyApp
                              {
                                                                    [HttpEndpoint(HttpVerb.Post, "/api/items")]
                                  public record CreateItemRequest(string Name);

                                  [GenerateService]
                                  public partial interface IMyService;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        runResult.GeneratedTrees.Should().HaveCount(1);

        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        // Only MyApp namespace scanned (default = interface namespace)
        code.Should().Contain("CreateItemAsync");
        code.Should().NotContain("OtherAsync");
    }

    [Fact]
    public void ScansCustomNamespaces()
    {
        const string source = """
                              using NOF.Contract;

                              namespace Other
                              {
                                                                    [HttpEndpoint(HttpVerb.Get, "/api/other")]
                                  public record OtherRequest;
                              }

                              namespace MyApp
                              {
                                                                    [HttpEndpoint(HttpVerb.Post, "/api/items")]
                                  public record CreateItemRequest(string Name);

                                  [GenerateService(Namespaces = new[] { "MyApp", "Other" })]
                                  public partial interface IMyService;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        runResult.GeneratedTrees.Should().HaveCount(1);

        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        code.Should().Contain("CreateItemAsync");
        code.Should().Contain("OtherAsync");
    }

    [Fact]
    public void GeneratesHttpClientRouteParamsForGet()
    {
        const string source = """
                              using NOF.Contract;

                              namespace MyApp
                              {
                                                                    [HttpEndpoint(HttpVerb.Get, "/api/users/{id}/posts")]
                                  public class GetUserPostsRequest
                                  {
                                      public int Id { get; set; }
                                      public int Page { get; set; }
                                      public int PageSize { get; set; }
                                  }

                                  [GenerateService]
                                  public partial interface IMyService;
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
    public void GeneratesHttpClientRouteParamsForPut()
    {
        const string source = """
                              using NOF.Contract;

                              namespace MyApp
                              {
                                                                    [HttpEndpoint(HttpVerb.Put, "/api/users/{id}")]
                                  public class UpdateUserRequest
                                  {
                                      public int Id { get; set; }
                                      public string Name { get; set; } = default!;
                                      public string Email { get; set; } = default!;
                                  }

                                  [GenerateService]
                                  public partial interface IMyService;
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
    public void GeneratesNoRouteParamHandlingWhenRouteHasNoPlaceholders()
    {
        const string source = """
                              using NOF.Contract;

                              namespace MyApp
                              {
                                                                    [HttpEndpoint(HttpVerb.Post, "/api/products")]
                                  public partial class CreateProductRequest
                                  {
                                      public string Name { get; set; } = default!;
                                  }

                                  [GenerateService]
                                  public partial interface IMyService;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        code.Should().Contain("JsonContent.Create(request,");
        code.Should().NotContain("Dictionary<string, object?>");
    }

    [Fact]
    public void PublicApiWithoutHttpEndpoint_DefaultsToPost()
    {
        const string source = """
                              using NOF.Contract;

                              namespace MyApp
                              {
                                                                    public record InternalRequest(string Data);

                                  [GenerateService]
                                  public partial interface IMyService;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        runResult.GeneratedTrees.Should().HaveCount(1);

        var root = runResult.GeneratedTrees[0].GetRoot();
        var code = root.ToFullString();

        // HTTP client generated
        root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Should().Contain(c => c.Identifier.Text == "HttpMyService");

        code.Should().Contain("InternalAsync");

        // HTTP client defaults to POST
        code.Should().Contain("HttpMethod.Post");
        // Body is the full request (no route params)
        code.Should().Contain("JsonContent.Create(request,");
    }

    [Fact]
    public void GenerateHttpClientFalse_GeneratesNoClientImplementation()
    {
        const string source = """
                              using NOF.Contract;

                              namespace MyApp
                              {
                                                                    [HttpEndpoint(HttpVerb.Post, "/api/items")]
                                  public record CreateItemRequest(string Name);

                                  [GenerateService(GenerateHttpClient = false)]
                                  public partial interface IMyService;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        runResult.GeneratedTrees.Should().HaveCount(1);

        var root = runResult.GeneratedTrees[0].GetRoot();

        root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Should().NotContain(c => c.Identifier.Text == "HttpMyService");
        root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Should().BeEmpty();
    }

    [Fact]
    public void NullableDateTimeQueryParam_UsesValueAccessor()
    {
        const string source = """
                              using NOF.Contract;
                              using System;

                              namespace MyApp
                              {
                                                                    [HttpEndpoint(HttpVerb.Get, "/api/reports")]
                                  public class GetReportsRequest
                                  {
                                      public DateTime? SubmitTimeFrom { get; set; }
                                      public DateTime? SubmitTimeTo { get; set; }
                                  }

                                  [GenerateService]
                                  public partial interface IMyService;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        code.Should().Contain("request.SubmitTimeFrom.Value.ToString(\"O\", global::System.Globalization.CultureInfo.InvariantCulture)");
        code.Should().Contain("request.SubmitTimeTo.Value.ToString(\"O\", global::System.Globalization.CultureInfo.InvariantCulture)");
        code.Should().Contain("if (request.SubmitTimeFrom is not null)");
        code.Should().Contain("if (request.SubmitTimeTo is not null)");
    }

    [Fact]
    public void NonNullableDateTimeQueryParam_DoesNotUseValueAccessor()
    {
        const string source = """
                              using NOF.Contract;
                              using System;

                              namespace MyApp
                              {
                                                                    [HttpEndpoint(HttpVerb.Get, "/api/logs")]
                                  public class GetLogsRequest
                                  {
                                      public DateTime Since { get; set; }
                                      public DateOnly Date { get; set; }
                                  }

                                  [GenerateService]
                                  public partial interface IMyService;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        code.Should().Contain("request.Since.ToString(\"O\", global::System.Globalization.CultureInfo.InvariantCulture)");
        code.Should().NotContain("request.Since.Value");
        code.Should().Contain("request.Date.ToString(\"yyyy-MM-dd\", global::System.Globalization.CultureInfo.InvariantCulture)");
        code.Should().NotContain("request.Date.Value");
        code.Should().NotContain("if (request.Since is not null)");
        code.Should().NotContain("if (request.Date is not null)");
    }

    [Fact]
    public void GeneratedCode_UsesFqnWithGlobalPrefix()
    {
        const string source = """
                              using NOF.Contract;

                              namespace MyApp
                              {
                                                                    [HttpEndpoint(HttpVerb.Get, "/api/items")]
                                  public record GetItemsRequest(int Page);

                                  [GenerateService]
                                  public partial interface IMyService;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, _extraRefs);
        var code = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        code.Should().Contain("global::System.Threading.Tasks.Task<");
        code.Should().Contain("global::System.Threading.CancellationToken");
        code.Should().Contain("global::System.Net.Http.HttpClient");
        code.Should().Contain("global::System.Text.Json.JsonSerializerOptions");
        code.Should().Contain("global::NOF.Contract.Result");
        code.Should().NotContain("global::NOF.Contract.IRequestSender");
        // No HttpCompletionOption
        code.Should().NotContain("HttpCompletionOption");
    }
}



