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
                                  [ExposeToHttpEndpoint(HttpVerb.Delete, AllowAnonymous = true)]
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
                                  [ExposeToHttpEndpoint(HttpVerb.Put, "/items/update", OperationName = "Update", Permission = "admin.write")]
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
}