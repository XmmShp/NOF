using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Application.SourceGenerator;
using NOF.Contract;
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
                public class MyCommandHandler : CommandHandler<MyCommand>
                {
                    public override System.Threading.Tasks.Task HandleAsync(MyCommand command, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(IServiceCollection),
            typeof(CommandHandler<>),
            typeof(ICommand),
            typeof(CommandHandlerInfo)
        );

        var result = new HandlerRegistrationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        Assert.Contains("[assembly: global::NOF.Annotation.AssemblyInitializeAttribute<global::App.__AppHandlerAssemblyInitializer>]", generatedCode);
        Assert.Contains("global::NOF.Application.HandlerRegistry.Register(new global::NOF.Application.CommandHandlerInfo(typeof(global::App.MyCommandHandler), typeof(global::App.MyCommand)));", generatedCode);
        Assert.DoesNotContain("SourceModule.ReferencedAssemblySymbols", generatedCode);

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
                public class MyNotificationHandler : NotificationHandler<MyNotification>
                {
                    public override System.Threading.Tasks.Task HandleAsync(MyNotification notification, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(IServiceCollection),
            typeof(NotificationHandler<>),
            typeof(INotification),
            typeof(NotificationHandlerInfo)
        );

        var result = new HandlerRegistrationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        Assert.Contains("[assembly: global::NOF.Annotation.AssemblyInitializeAttribute<global::App.__AppHandlerAssemblyInitializer>]", generatedCode);
        Assert.Contains("global::NOF.Application.HandlerRegistry.Register(new global::NOF.Application.NotificationHandlerInfo(typeof(global::App.MyNotificationHandler), typeof(global::App.MyNotification)));", generatedCode);
        Assert.DoesNotContain("SourceModule.ReferencedAssemblySymbols", generatedCode);
    }
}
