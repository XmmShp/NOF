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
            using NOF.Abstraction;
            using NOF.Contract;
            namespace App
            {
                public record MyCommand;
                public class MyCommandHandler : CommandHandler<MyCommand>
                {
                    public override System.Threading.Tasks.Task HandleAsync(MyCommand command, Context context, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(IServiceCollection),
            typeof(Context),
            typeof(CommandHandler<>),
            typeof(CommandHandlerRegistration)
        );

        var result = new HandlerRegistrationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        Assert.Contains("[assembly: global::NOF.Annotation.AssemblyInitializeAttribute<global::App.__AppHandlerAssemblyInitializer>]", generatedCode);
        Assert.Contains("registry.IsInitialized.TryAdd(typeof(__AppHandlerAssemblyInitializer), true)", generatedCode);
        Assert.Contains("registry.CommandHandlerRegistry.Add(new global::NOF.Application.CommandHandlerRegistration(typeof(global::App.MyCommandHandler), typeof(global::App.MyCommand)));", generatedCode);
        Assert.DoesNotContain("SourceModule.ReferencedAssemblySymbols", generatedCode);

    }

    [Fact]
    public void GeneratedCode_NotificationHandler_RegistersBothConcreteAndInterfaceFactory()
    {
        const string source = """
            using NOF.Application;
            using NOF.Abstraction;
            using NOF.Contract;
            namespace App
            {
                public record MyNotification;
                public class MyNotificationHandler : NotificationHandler<MyNotification>
                {
                    public override System.Threading.Tasks.Task HandleAsync(MyNotification notification, Context context, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(IServiceCollection),
            typeof(Context),
            typeof(NotificationHandler<>),
            typeof(NotificationHandlerRegistration)
        );

        var result = new HandlerRegistrationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        Assert.Contains("[assembly: global::NOF.Annotation.AssemblyInitializeAttribute<global::App.__AppHandlerAssemblyInitializer>]", generatedCode);
        Assert.Contains("registry.IsInitialized.TryAdd(typeof(__AppHandlerAssemblyInitializer), true)", generatedCode);
        Assert.Contains("registry.NotificationHandlerRegistry.Add(new global::NOF.Application.NotificationHandlerRegistration(typeof(global::App.MyNotificationHandler), typeof(global::App.MyNotification)));", generatedCode);
        Assert.DoesNotContain("SourceModule.ReferencedAssemblySymbols", generatedCode);
    }
}
