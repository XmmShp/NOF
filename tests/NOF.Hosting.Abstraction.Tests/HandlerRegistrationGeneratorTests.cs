using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using NOF.Hosting.SourceGenerator;
using NOF.Infrastructure;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class HandlerRegistrationGeneratorTests
{
    [Fact]
    public void GeneratedCode_CommandHandler_RegistersConcreteOnly()
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

        // Should use typed CommandHandlerInfo
        generatedCode.Should().Contain("new global::NOF.Infrastructure.CommandHandlerInfo(typeof(global::App.MyCommandHandler), typeof(global::App.MyCommand))");

        // Keyed service registration is handled at runtime by AddHandlerInfo, not in generated code
        generatedCode.Should().NotContain("AddKeyedScoped");

    }

    [Fact]
    public void GeneratedCode_RequestHandler_RegistersConcreteOnly()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;
            namespace App
            {
                public record MyRequest;
                public class MyRequestHandler : IRequestHandler<MyRequest, string>
                {
                    public System.Threading.Tasks.Task<Result<string>> HandleAsync(MyRequest request, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(IServiceCollection),
            typeof(IRequestHandler<,>),            typeof(Result),
            typeof(RequestWithResponseHandlerInfo)
        );

        var result = new HandlerRegistrationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        // Should use typed RequestWithResponseHandlerInfo
        generatedCode.Should().Contain("new global::NOF.Infrastructure.RequestWithResponseHandlerInfo(typeof(global::App.MyRequestHandler), typeof(global::App.MyRequest), typeof(string))");

        // Keyed service registration is handled at runtime by AddHandlerInfo, not in generated code
        generatedCode.Should().NotContain("AddKeyedScoped");

    }

    [Fact]
    public void GeneratedCode_EventHandler_RegistersBothConcreteAndInterfaceFactory()
    {
        const string source = """
            using NOF.Application;
            using NOF.Domain;
            namespace App
            {
                public record MyEvent : IEvent;
                public class MyEventHandler : IEventHandler<MyEvent>
                {
                    public System.Threading.Tasks.Task HandleAsync(MyEvent @event, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(IServiceCollection),
            typeof(IEventHandler<>),
            typeof(IEvent),
            typeof(EventHandlerInfo)
        );

        var result = new HandlerRegistrationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        // Should use typed EventHandlerInfo
        generatedCode.Should().Contain("new global::NOF.Infrastructure.EventHandlerInfo(typeof(global::App.MyEventHandler), typeof(global::App.MyEvent))");

        // Keyed service registration is handled at runtime by AddHandlerInfo, not in generated code
        generatedCode.Should().NotContain("AddKeyedScoped");

    }

    [Fact]
    public void GeneratedCode_NotificationHandler_RegistersBothConcreteAndInterfaceFactory()
    {
        const string source = """
            using NOF.Application;
            using NOF.Contract;
            namespace App
            {
                public record MyNotification : INotification;
                public class MyNotificationHandler : INotificationHandler<MyNotification>
                {
                    public System.Threading.Tasks.Task HandleAsync(MyNotification notification, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(IServiceCollection),
            typeof(INotificationHandler<>),
            typeof(INotification),
            typeof(NotificationHandlerInfo)
        );

        var result = new HandlerRegistrationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        // Should use typed NotificationHandlerInfo
        generatedCode.Should().Contain("new global::NOF.Infrastructure.NotificationHandlerInfo(typeof(global::App.MyNotificationHandler), typeof(global::App.MyNotification))");

        // Keyed service registration is handled at runtime by AddHandlerInfo, not in generated code
        generatedCode.Should().NotContain("AddKeyedScoped");
    }
}


