using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting.SourceGenerator;
using NOF.Infrastructure.Abstraction;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class HandlerRegistrationGeneratorTests
{
    [Fact]
    public void GeneratedCode_UsesFqnForTypedHandlerInfo()
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
            typeof(CommandHandlerInfo)
        );

        var result = new HandlerRegistrationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        // Should use global:: FQN for typed CommandHandlerInfo
        generatedCode.Should().Contain("global::NOF.Infrastructure.Abstraction.CommandHandlerInfo");

        // Should register as keyed service
        generatedCode.Should().Contain("global::NOF.Infrastructure.Abstraction.CommandHandlerKey.Of");

        // Should use global:: FQN for IServiceCollection in method signature
        generatedCode.Should().Contain("global::Microsoft.Extensions.DependencyInjection.IServiceCollection");

        // Should keep using NOF.Infrastructure.Abstraction for AddHandlerInfo extension method
        generatedCode.Should().Contain("using NOF.Infrastructure.Abstraction;");
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
            typeof(RequestWithResponseHandlerInfo)
        );

        var result = new HandlerRegistrationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        generatedCode.Should().Contain("global::NOF.Infrastructure.Abstraction.RequestWithResponseHandlerInfo");
        generatedCode.Should().Contain("global::NOF.Infrastructure.Abstraction.RequestWithResponseHandlerKey.Of");
        generatedCode.Should().Contain("typeof(global::App.MyRequestHandler)");
    }
}
