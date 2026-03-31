using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Application.SourceGenerator;
using NOF.Contract;
using NOF.Domain;
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

        generatedCode.Should().Contain("[assembly: global::NOF.Annotation.AssemblyInitializeAttribute<global::App.__AppHandlerAssemblyInitializer>]");
        generatedCode.Should().Contain("global::NOF.Application.HandlerRegistry.Register(new global::NOF.Application.CommandHandlerInfo(typeof(global::App.MyCommandHandler), typeof(global::App.MyCommand)));");
        generatedCode.Should().NotContain("SourceModule.ReferencedAssemblySymbols");

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

        generatedCode.Should().Contain("[assembly: global::NOF.Annotation.AssemblyInitializeAttribute<global::App.__AppHandlerAssemblyInitializer>]");
        generatedCode.Should().Contain("global::NOF.Application.HandlerRegistry.Register(new global::NOF.Application.EventHandlerInfo(typeof(global::App.MyEventHandler), typeof(global::App.MyEvent)));");
        generatedCode.Should().NotContain("SourceModule.ReferencedAssemblySymbols");

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

        generatedCode.Should().Contain("[assembly: global::NOF.Annotation.AssemblyInitializeAttribute<global::App.__AppHandlerAssemblyInitializer>]");
        generatedCode.Should().Contain("global::NOF.Application.HandlerRegistry.Register(new global::NOF.Application.NotificationHandlerInfo(typeof(global::App.MyNotificationHandler), typeof(global::App.MyNotification)));");
        generatedCode.Should().NotContain("SourceModule.ReferencedAssemblySymbols");
    }
}

