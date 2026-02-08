using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NOF.Contract.SourceGenerator;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class ExposeToHttpEndpointServiceGeneratorTests
{

    [Fact]
    public void GeneratesClientForBasicGenericRequestWithDefaultRoute()
    {
        const string source = """

                              using NOF;

                              namespace MyApp
                              {
                                  [ExposeToHttpEndpoint(HttpVerb.Post)]
                                  public partial class CreateUserRequest : IRequest<string>
                                  {
                                      public string Name { get; set; } = default!;
                                  }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, typeof(ExposeToHttpEndpointAttribute));
        runResult.GeneratedTrees.Should().HaveCount(1);

        var clientTree = runResult.GeneratedTrees[0];

        var clientRoot = clientTree.GetRoot();

        // Client: interface and implementation
        clientRoot.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
            .Should().Contain(i => i.Identifier.Text == "IMyAppService");

        clientRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Should().Contain(c => c.Identifier.Text == "MyAppServiceClient");

        clientRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Should().Contain(m =>
                m.Identifier.Text == "CreateUserAsync"
                && m.ParameterList.Parameters.Count == 3
                && m.ParameterList.Parameters[0].Type!.ToString() == "MyApp.CreateUserRequest");
    }

    [Fact]
    public void GeneratesClientWithCustomOperationNameAndExplicitRoute()
    {
        const string source = """

                              using NOF;

                              namespace MyApi
                              {
                                  [ExposeToHttpEndpoint(HttpVerb.Get, "/users/profile", OperationName = "GetProfile")]
                                  public partial class UserProfileRequest : IRequest<UserDto>
                                  {
                                  }

                                  public class UserDto { public string Name { get; set; } = default!; }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, typeof(ExposeToHttpEndpointAttribute));
        runResult.GeneratedTrees.Should().HaveCount(1);

        var clientTree = runResult.GeneratedTrees[0];

        var clientRoot = clientTree.GetRoot();

        clientRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Should().Contain(m =>
                m.Identifier.Text == "GetProfileAsync"
                && m.ParameterList.Parameters.Count == 3
                && m.ParameterList.Parameters[0].Type!.ToString() == "MyApi.UserProfileRequest");
    }

    [Fact]
    public void GeneratesClientForNonGenericRequestWithAllowAnonymous()
    {
        const string source = """

                              using NOF;

                              namespace Tasks
                              {
                                  [ExposeToHttpEndpoint(HttpVerb.Delete)]
                                  public partial class DeleteTaskRequest : IRequest
                                  {
                                      public int Id { get; set; }
                                  }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, typeof(ExposeToHttpEndpointAttribute));
        runResult.GeneratedTrees.Should().HaveCount(1);

        var clientTree = runResult.GeneratedTrees[0];

        var clientRoot = clientTree.GetRoot();

        clientRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Should().Contain(m =>
                m.Identifier.Text == "DeleteTaskAsync"
                && m.ParameterList.Parameters.Count == 3
                && m.ParameterList.Parameters[0].Type!.ToString() == "Tasks.DeleteTaskRequest");
    }

    [Fact]
    public void GeneratesMultipleMethodsForClassWithMultipleExposeAttributes()
    {
        const string source = """

                              using NOF;

                              namespace Items
                              {
                                  [ExposeToHttpEndpoint(HttpVerb.Post, OperationName = "Create")]
                                  [ExposeToHttpEndpoint(HttpVerb.Put, "/items/update", OperationName = "Update")]
                                  public partial class ItemRequest : IRequest<ItemResponse>
                                  {
                                      public string Data { get; set; } = default!;
                                  }

                                  public class ItemResponse { public int Id { get; set; } }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, typeof(ExposeToHttpEndpointAttribute));
        runResult.GeneratedTrees.Should().HaveCount(1);

        var clientTree = runResult.GeneratedTrees[0];

        var clientRoot = clientTree.GetRoot();

        // Two methods in client
        clientRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Should().Contain(m =>
                m.Identifier.Text == "CreateAsync"
                && m.ParameterList.Parameters[0].Type!.ToString() == "Items.ItemRequest");

        clientRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Should().Contain(m =>
                m.Identifier.Text == "UpdateAsync"
                && m.ParameterList.Parameters[0].Type!.ToString() == "Items.ItemRequest");
    }

    [Fact]
    public void UsesRootNamespaceForServiceNameInNestedNamespace()
    {
        const string source = """

                              using NOF;

                              namespace MyApp.Features.Users.Api
                              {
                                  [ExposeToHttpEndpoint(HttpVerb.Get)]
                                  public partial class GetUserRequest : IRequest<UserInfo>
                                  {
                                      public int Id { get; set; }
                                  }

                                  public class UserInfo { public string Email { get; set; } = default!; }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, typeof(ExposeToHttpEndpointAttribute));
        runResult.GeneratedTrees.Should().HaveCount(1);

        var clientTree = runResult.GeneratedTrees[0];

        var clientRoot = clientTree.GetRoot();

        // Service name based on root namespace "MyApp"
        clientRoot.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
            .Should().Contain(i => i.Identifier.Text == "IMyAppService");

        clientRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Should().Contain(c => c.Identifier.Text == "MyAppServiceClient");

        clientRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Should().Contain(m =>
                m.Identifier.Text == "GetUserAsync"
                && m.ParameterList.Parameters[0].Type!.ToString() == "MyApp.Features.Users.Api.GetUserRequest");
    }

    [Fact]
    public void DoesNotGenerateCodeForClassesWithoutIRequestOrAttribute()
    {
        const string source = """

                              using NOF;

                              namespace Ignored
                              {
                                  [ExposeToHttpEndpoint(HttpVerb.Post)]
                                  public class PlainClass { }

                                  public class SilentRequest : IRequest<string> { }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, typeof(ExposeToHttpEndpointAttribute));
        runResult.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void GeneratesRouteParameterFillingForGetRequest()
    {
        const string source = """

                              using NOF;

                              namespace Users
                              {
                                  [ExposeToHttpEndpoint(HttpVerb.Get, "/api/users/{id}/posts")]
                                  public class GetUserPostsRequest : IRequest<string>
                                  {
                                      public int Id { get; set; }
                                      public int Page { get; set; }
                                      public int PageSize { get; set; }
                                  }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, typeof(ExposeToHttpEndpointAttribute));
        runResult.GeneratedTrees.Should().HaveCount(1);

        var generatedCode = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        // Route param should be interpolated into the URL
        generatedCode.Should().Contain("Uri.EscapeDataString(request.Id.ToString()!)");

        // Non-route params should go to query string
        generatedCode.Should().Contain("queryParts");
        generatedCode.Should().Contain("Page");
        generatedCode.Should().Contain("PageSize");

        // Should NOT contain the raw {id} placeholder
        generatedCode.Should().NotContain("\"{/api/users/{id}/posts}\"");
    }

    [Fact]
    public void GeneratesRouteParameterFillingForPostRequest()
    {
        const string source = """

                              using NOF;

                              namespace Users
                              {
                                  [ExposeToHttpEndpoint(HttpVerb.Put, "/api/users/{id}")]
                                  public class UpdateUserRequest : IRequest
                                  {
                                      public int Id { get; set; }
                                      public string Name { get; set; } = default!;
                                      public string Email { get; set; } = default!;
                                  }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, typeof(ExposeToHttpEndpointAttribute));
        runResult.GeneratedTrees.Should().HaveCount(1);

        var generatedCode = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        // Route param should be interpolated
        generatedCode.Should().Contain("Uri.EscapeDataString(request.Id.ToString()!)");

        // Non-route params should go to body dictionary
        generatedCode.Should().Contain("Dictionary<string, object?>");
        generatedCode.Should().Contain("body[\"Name\"] = request.Name");
        generatedCode.Should().Contain("body[\"Email\"] = request.Email");

        // Body should NOT contain the route param
        generatedCode.Should().NotContain("body[\"Id\"]");
    }

    [Fact]
    public void GeneratesRouteParametersCaseInsensitive()
    {
        const string source = """

                              using NOF;

                              namespace Orders
                              {
                                  [ExposeToHttpEndpoint(HttpVerb.Get, "/api/orders/{orderId}/items/{itemId}")]
                                  public record GetOrderItemRequest(string OrderId, string ItemId) : IRequest<string>;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, typeof(ExposeToHttpEndpointAttribute));
        runResult.GeneratedTrees.Should().HaveCount(1);

        var generatedCode = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        // Both route params should be interpolated
        generatedCode.Should().Contain("Uri.EscapeDataString(request.OrderId");
        generatedCode.Should().Contain("Uri.EscapeDataString(request.ItemId");

        // No query string needed since all properties are route params
        generatedCode.Should().NotContain("queryParts");
    }

    [Fact]
    public void GeneratesNoRouteParamHandlingWhenRouteHasNoPlaceholders()
    {
        const string source = """

                              using NOF;

                              namespace Products
                              {
                                  [ExposeToHttpEndpoint(HttpVerb.Post, "/api/products")]
                                  public partial class CreateProductRequest : IRequest<int>
                                  {
                                      public string Name { get; set; } = default!;
                                  }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, typeof(ExposeToHttpEndpointAttribute));
        runResult.GeneratedTrees.Should().HaveCount(1);

        var generatedCode = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        // Should use the full request object as body, not a dictionary
        generatedCode.Should().Contain("JsonContent.Create(request,");
        generatedCode.Should().NotContain("Dictionary<string, object?>");
    }

    [Fact]
    public void RecordWithPrimaryCtorAndExtraProps_AllPropertiesVisible()
    {
        // Record primary ctor params become properties (readable), extra props also visible
        const string source = """

                              using NOF;

                              namespace Items
                              {
                                  [ExposeToHttpEndpoint(HttpVerb.Patch, "/api/items/{id}")]
                                  public record UpdateItemRequest(long Id) : IRequest
                                  {
                                      public string? Value { get; set; }
                                      public int? Priority { get; set; }
                                  }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, typeof(ExposeToHttpEndpointAttribute));
        runResult.GeneratedTrees.Should().HaveCount(1);

        var generatedCode = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        // Route param Id should be interpolated into URL
        generatedCode.Should().Contain("Uri.EscapeDataString(request.Id.ToString()!)");

        // Non-route props should go to body dictionary
        generatedCode.Should().Contain("Dictionary<string, object?>");
        generatedCode.Should().Contain("body[\"Value\"] = request.Value");
        generatedCode.Should().Contain("body[\"Priority\"] = request.Priority");

        // Route param should NOT be in body
        generatedCode.Should().NotContain("body[\"Id\"]");
    }

    [Fact]
    public void RecordWithAllPropsInCtor_RouteAndBodyCorrectlySplit()
    {
        // All properties come from primary ctor — all are readable
        const string source = """

                              using NOF;

                              namespace Files
                              {
                                  [ExposeToHttpEndpoint(HttpVerb.Put, "/api/nodes/{nodeId}/files/{fileName}")]
                                  public record AddFileRequest(long NodeId, string FileName, string Content) : IRequest;
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, typeof(ExposeToHttpEndpointAttribute));
        runResult.GeneratedTrees.Should().HaveCount(1);

        var generatedCode = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        // Route params should be interpolated
        generatedCode.Should().Contain("Uri.EscapeDataString(request.NodeId.ToString()!)");
        generatedCode.Should().Contain("Uri.EscapeDataString(request.FileName");

        // Only Content should go to body
        generatedCode.Should().Contain("body[\"Content\"] = request.Content");
        generatedCode.Should().NotContain("body[\"NodeId\"]");
        generatedCode.Should().NotContain("body[\"FileName\"]");
    }

    [Fact]
    public void ClassWithPrimaryCtorParams_ParamsNotVisibleAsProperties()
    {
        // Class primary ctor params are NOT properties — they won't be found by GetAllPublicProperties
        // Only explicitly declared properties are visible
        const string source = """

                              using NOF;

                              namespace Items
                              {
                                  [ExposeToHttpEndpoint(HttpVerb.Put, "/api/items/{id}")]
                                  public class UpdateItemClassRequest(long id) : IRequest
                                  {
                                      public string Name { get; set; } = default!;
                                      public int Count { get; set; }
                                  }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, typeof(ExposeToHttpEndpointAttribute));
        runResult.GeneratedTrees.Should().HaveCount(1);

        var generatedCode = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        // Class primary ctor param 'id' (lowercase) is NOT a property, so GetAllPublicProperties won't find it.
        // The route has {id} but there's no matching public property — hasRouteParams is false.
        // Therefore the generator falls back to serializing the whole request as body (no dictionary).
        generatedCode.Should().Contain("JsonContent.Create(request,");
        generatedCode.Should().NotContain("Dictionary<string, object?>");

        // The route placeholder {id} is NOT interpolated — it stays as a literal string
        generatedCode.Should().Contain("var endpoint = \"/api/items/{id}\"");
    }

    [Fact]
    public void ClassWithExplicitPropsMatchingRoute_WorksCorrectly()
    {
        // Class with explicit properties that match route params
        const string source = """

                              using NOF;

                              namespace Items
                              {
                                  [ExposeToHttpEndpoint(HttpVerb.Put, "/api/items/{id}")]
                                  public class UpdateItemRequest : IRequest
                                  {
                                      public long Id { get; set; }
                                      public string Name { get; set; } = default!;
                                  }
                              }
                              """;

        var runResult = new ExposeToHttpEndpointServiceGenerator().GetResult(source, typeof(ExposeToHttpEndpointAttribute));
        runResult.GeneratedTrees.Should().HaveCount(1);

        var generatedCode = runResult.GeneratedTrees[0].GetRoot().ToFullString();

        // Id should be interpolated into URL
        generatedCode.Should().Contain("Uri.EscapeDataString(request.Id.ToString()!)");

        // Name should go to body
        generatedCode.Should().Contain("body[\"Name\"] = request.Name");

        // Id should NOT be in body
        generatedCode.Should().NotContain("body[\"Id\"]");
    }
}
