using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting.SourceGenerator;
using NOF.Infrastructure.Core;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class HandlerRegistrationGeneratorTests
{
    [Fact]
    public void GeneratedCode_UsesFqnForHandlerInfoAndHandlerKind()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;
            namespace App
            {
                public record MyCommand : ICommand;
                public class MyCommandHandler : ICommandHandler<MyCommand>
                {
                    public System.Threading.Tasks.Task HandleAsync(MyCommand command, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(IServiceCollection),
            typeof(ICommandHandler<>),
            typeof(ICommand),
            typeof(HandlerInfo)
        );

        var result = new HandlerRegistrationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        // Should use global:: FQN for HandlerInfo and HandlerKind
        generatedCode.Should().Contain("global::NOF.Infrastructure.Core.HandlerInfo");
        generatedCode.Should().Contain("global::NOF.Infrastructure.Core.HandlerKind.Command");

        // Should use global:: FQN for IServiceCollection in method signature
        generatedCode.Should().Contain("global::Microsoft.Extensions.DependencyInjection.IServiceCollection");

        // Should keep using NOF.Infrastructure.Core for AddHandlerInfo extension method
        generatedCode.Should().Contain("using NOF.Infrastructure.Core;");
    }

    [Fact]
    public void GeneratedCode_RegistersRequestHandlerWithResponseType()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;
            namespace App
            {
                public record MyRequest : IRequest<string>;
                public class MyRequestHandler : IRequestHandler<MyRequest, string>
                {
                    public System.Threading.Tasks.Task<Result<string>> HandleAsync(MyRequest request, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(IServiceCollection),
            typeof(IRequestHandler<,>),
            typeof(NOF.Contract.IRequest<>),
            typeof(NOF.Contract.Result),
            typeof(HandlerInfo)
        );

        var result = new HandlerRegistrationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        generatedCode.Should().Contain("global::NOF.Infrastructure.Core.HandlerKind.RequestWithResponse");
        generatedCode.Should().Contain("typeof(global::App.MyRequestHandler)");
    }
}
