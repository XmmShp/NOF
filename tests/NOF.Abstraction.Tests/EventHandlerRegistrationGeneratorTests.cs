using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction.SourceGenerator;
using NOF.SourceGenerator.Tests;
using NOF.SourceGenerator.Tests.Extensions;
using Xunit;

namespace NOF.Abstraction.Tests;

public class EventHandlerRegistrationGeneratorTests
{
    [Fact]
    public void GeneratedCode_EventHandler_RegistersToEventHandlerRegistry()
    {
        const string source = """
            using NOF.Abstraction;
            namespace App
            {
                public record MyEvent;
                public class MyEventHandler : IEventHandler<MyEvent>
                {
                    public System.Threading.Tasks.Task HandleAsync(MyEvent @event, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
                }
            }
            """;

        var comp = CSharpCompilation.CreateCompilation("App", source, isDll: true,
            typeof(IServiceCollection),
            typeof(IEventHandler<>),
            typeof(EventHandlerRegistration));

        var result = new EventHandlerRegistrationGenerator().GetResult(comp);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        Assert.Contains("[assembly: global::NOF.Annotation.AssemblyInitializeAttribute<global::App.__AppEventHandlerAssemblyInitializer>]", generatedCode);
        Assert.Contains("global::NOF.Abstraction.EventHandlerRegistry.Register(new global::NOF.Abstraction.EventHandlerRegistration(typeof(global::App.MyEventHandler), typeof(global::App.MyEvent)));", generatedCode);
        Assert.DoesNotContain("SourceModule.ReferencedAssemblySymbols", generatedCode);
    }
}
