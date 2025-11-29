using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class ExposeToHttpEndpointGeneratorTests
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

        var runResult = new ExposeToHttpEndpointGenerator().GetResult<ExposeToHttpEndpointAttribute>(source);
        runResult.GeneratedTrees.Should().HaveCount(2);

        var clientTree = runResult.GeneratedTrees[0];
        var endpointsTree = runResult.GeneratedTrees[1];

        var clientRoot = clientTree.GetRoot();
        var endpointsRoot = endpointsTree.GetRoot();

        // Client: interface and implementation
        clientRoot.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
            .Should().Contain(i => i.Identifier.Text == "IMyAppService");

        clientRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Should().Contain(c => c.Identifier.Text == "MyAppServiceClient");

        clientRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Should().Contain(m =>
                m.Identifier.Text == "CreateUserAsync"
                && m.ParameterList.Parameters.Count == 1
                && m.ParameterList.Parameters[0].Type!.ToString() == "MyApp.CreateUserRequest");

        // Endpoints: HttpEndpoint construction
        endpointsRoot.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>()
            .Should().Contain(obj =>
                obj.ArgumentList.Arguments.Count >= 5
                && obj.ArgumentList.Arguments[0].Expression.ToString() == "typeof(MyApp.CreateUserRequest)"
                && obj.ArgumentList.Arguments[1].Expression.ToString() == "HttpVerb.Post"
                && obj.ArgumentList.Arguments[2].Expression.ToString() == "\"CreateUser\""
                && obj.ArgumentList.Arguments[4].Expression.ToString() == "false");
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

        var runResult = new ExposeToHttpEndpointGenerator().GetResult<ExposeToHttpEndpointAttribute>(source);
        runResult.GeneratedTrees.Should().HaveCount(2);

        var clientTree = runResult.GeneratedTrees[0];
        var endpointsTree = runResult.GeneratedTrees[1];

        var clientRoot = clientTree.GetRoot();
        var endpointsRoot = endpointsTree.GetRoot();

        clientRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Should().Contain(m =>
                m.Identifier.Text == "GetProfileAsync"
                && m.ParameterList.Parameters.Count == 1
                && m.ParameterList.Parameters[0].Type!.ToString() == "MyApi.UserProfileRequest");

        endpointsRoot.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>()
            .Should().Contain(obj =>
                obj.ArgumentList.Arguments.Count >= 5
                && obj.ArgumentList.Arguments[0].Expression.ToString() == "typeof(MyApi.UserProfileRequest)"
                && obj.ArgumentList.Arguments[1].Expression.ToString() == "HttpVerb.Get"
                && obj.ArgumentList.Arguments[2].Expression.ToString() == "\"/users/profile\""
                && obj.ArgumentList.Arguments[4].Expression.ToString() == "false");
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

        var runResult = new ExposeToHttpEndpointGenerator().GetResult<ExposeToHttpEndpointAttribute>(source);
        runResult.GeneratedTrees.Should().HaveCount(2);

        var clientTree = runResult.GeneratedTrees[0];
        var endpointsTree = runResult.GeneratedTrees[1];

        var clientRoot = clientTree.GetRoot();
        var endpointsRoot = endpointsTree.GetRoot();

        clientRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Should().Contain(m =>
                m.Identifier.Text == "DeleteTaskAsync"
                && m.ParameterList.Parameters.Count == 1
                && m.ParameterList.Parameters[0].Type!.ToString() == "Tasks.DeleteTaskRequest");

        endpointsRoot.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>()
            .Should().Contain(obj =>
                obj.ArgumentList.Arguments.Count >= 5
                && obj.ArgumentList.Arguments[0].Expression.ToString() == "typeof(Tasks.DeleteTaskRequest)"
                && obj.ArgumentList.Arguments[1].Expression.ToString() == "HttpVerb.Delete"
                && obj.ArgumentList.Arguments[2].Expression.ToString() == "\"DeleteTask\""
                && obj.ArgumentList.Arguments[4].Expression.ToString() == "true"); // AllowAnonymous = true
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

        var runResult = new ExposeToHttpEndpointGenerator().GetResult<ExposeToHttpEndpointAttribute>(source);
        runResult.GeneratedTrees.Should().HaveCount(2);

        var clientTree = runResult.GeneratedTrees[0];
        var endpointsTree = runResult.GeneratedTrees[1];

        var clientRoot = clientTree.GetRoot();
        var endpointsRoot = endpointsTree.GetRoot();

        // Two methods in client
        clientRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Should().Contain(m =>
                m.Identifier.Text == "CreateAsync"
                && m.ParameterList.Parameters[0].Type!.ToString() == "Items.ItemRequest");

        clientRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Should().Contain(m =>
                m.Identifier.Text == "UpdateAsync"
                && m.ParameterList.Parameters[0].Type!.ToString() == "Items.ItemRequest");

        // Two HttpEndpoint objects
        var endpointCreations = endpointsRoot
            .DescendantNodes()
            .OfType<ImplicitObjectCreationExpressionSyntax>()
            .ToList();

        endpointCreations.Should().HaveCount(2);

        endpointCreations.Should().Contain(obj =>
            obj.ArgumentList.Arguments.Count >= 5
            && obj.ArgumentList.Arguments[0].Expression.ToString() == "typeof(Items.ItemRequest)"
            && obj.ArgumentList.Arguments[1].Expression.ToString() == "HttpVerb.Post"
            && obj.ArgumentList.Arguments[2].Expression.ToString() == "\"Create\""
            && obj.ArgumentList.Arguments[3].Expression.ToString() == "null"
            && obj.ArgumentList.Arguments[4].Expression.ToString() == "false");

        endpointCreations.Should().Contain(obj =>
            obj.ArgumentList.Arguments.Count >= 5
            && obj.ArgumentList.Arguments[0].Expression.ToString() == "typeof(Items.ItemRequest)"
            && obj.ArgumentList.Arguments[1].Expression.ToString() == "HttpVerb.Put"
            && obj.ArgumentList.Arguments[2].Expression.ToString() == "\"/items/update\""
            && obj.ArgumentList.Arguments[3].Expression.ToString() == "\"admin.write\""
            && obj.ArgumentList.Arguments[4].Expression.ToString() == "false");
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

        var runResult = new ExposeToHttpEndpointGenerator().GetResult<ExposeToHttpEndpointAttribute>(source);
        runResult.GeneratedTrees.Should().HaveCount(2);

        var clientTree = runResult.GeneratedTrees[0];
        var endpointsTree = runResult.GeneratedTrees[1];

        var clientRoot = clientTree.GetRoot();
        var endpointsRoot = endpointsTree.GetRoot();

        // Service name based on root namespace "MyApp"
        clientRoot.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
            .Should().Contain(i => i.Identifier.Text == "IMyAppService");

        clientRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Should().Contain(c => c.Identifier.Text == "MyAppServiceClient");

        clientRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Should().Contain(m =>
                m.Identifier.Text == "GetUserAsync"
                && m.ParameterList.Parameters[0].Type!.ToString() == "MyApp.Features.Users.Api.GetUserRequest");

        endpointsRoot.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>()
            .Should().Contain(obj =>
                obj.ArgumentList.Arguments.Count >= 5
                && obj.ArgumentList.Arguments[0].Expression.ToString() == "typeof(MyApp.Features.Users.Api.GetUserRequest)"
                && obj.ArgumentList.Arguments[1].Expression.ToString() == "HttpVerb.Get"
                && obj.ArgumentList.Arguments[2].Expression.ToString() == "\"GetUser\""
                && obj.ArgumentList.Arguments[4].Expression.ToString() == "false");
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

        var runResult = new ExposeToHttpEndpointGenerator().GetResult<ExposeToHttpEndpointAttribute>(source);
        runResult.GeneratedTrees.Should().BeEmpty();
    }
}