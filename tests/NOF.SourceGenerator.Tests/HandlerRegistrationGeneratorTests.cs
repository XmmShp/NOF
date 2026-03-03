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
        generatedCode.Should().Contain("global::NOF.Infrastructure.Abstraction.CommandHandlerInfo");

        // Point-to-point: registers concrete type only (no interface)
        generatedCode.Should().Contain("AddKeyedScoped<global::App.MyCommandHandler>(global::NOF.Infrastructure.Abstraction.CommandHandlerKey.Of(");
        generatedCode.Should().NotContain("AddKeyedScoped<global::NOF.Application.ICommandHandler>");

        // Should populate EndpointNameRegistry
        generatedCode.Should().Contain("EndpointNameRegistry");
    }

    [Fact]
    public void GeneratedCode_RequestHandler_RegistersConcreteOnly()
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

        // Point-to-point: registers concrete type only
        generatedCode.Should().Contain("AddKeyedScoped<global::App.MyRequestHandler>(global::NOF.Infrastructure.Abstraction.RequestWithResponseHandlerKey.Of(");
        generatedCode.Should().NotContain("AddKeyedScoped<global::NOF.Application.IRequestHandler>");
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
            typeof(NOF.Domain.IEvent),
            typeof(EventHandlerInfo)
        );

        var result = new HandlerRegistrationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        // Multicast: registers both concrete and interface factory
        generatedCode.Should().Contain("AddKeyedScoped<global::App.MyEventHandler>(global::NOF.Infrastructure.Abstraction.EventHandlerKey.Of(");
        generatedCode.Should().Contain("AddKeyedScoped<global::NOF.Application.IEventHandler>(global::NOF.Infrastructure.Abstraction.EventHandlerKey.Of(");
        generatedCode.Should().Contain("GetRequiredKeyedService<global::App.MyEventHandler>(key)");

        // Event handlers should NOT register endpoint names
        generatedCode.Should().NotContain("EndpointNameRegistry");
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

        // Multicast: registers both concrete and interface factory
        generatedCode.Should().Contain("AddKeyedScoped<global::App.MyNotificationHandler>(global::NOF.Infrastructure.Abstraction.NotificationHandlerKey.Of(");
        generatedCode.Should().Contain("AddKeyedScoped<global::NOF.Application.INotificationHandler>(global::NOF.Infrastructure.Abstraction.NotificationHandlerKey.Of(");
        generatedCode.Should().Contain("GetRequiredKeyedService<global::App.MyNotificationHandler>(key)");
    }
}
