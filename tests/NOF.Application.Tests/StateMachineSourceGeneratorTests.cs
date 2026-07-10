using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NOF.Abstraction;
using NOF.Application;
using NOF.Application.SourceGenerator;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public sealed class StateMachineSourceGeneratorTests
{
    [Fact]
    public void GeneratedCode_StateMachineNotificationHandlers_RegisterTypesWithTypeRegistry()
    {
        const string source = """
            using NOF.Application;

            namespace App;

            public enum OrderState
            {
                Pending,
                Completed
            }

            public sealed record OrderCreatedNotification(string OrderId);

            public sealed class OrderStateMachine : IStateMachineDefinition<OrderState>
            {
                public void Build(IStateMachineBuilder<OrderState> builder)
                {
                    builder.Correlate<OrderCreatedNotification>(notification => notification.OrderId);
                    builder.StartWhen<OrderCreatedNotification>(OrderState.Pending);
                }
            }
            """;

        var compilation = CSharpCompilation.CreateCompilation(
            "App",
            source,
            isDll: true,
            typeof(InitializedTypes),
            typeof(IStateMachineDefinition<>),
            typeof(IStateMachineBuilder<>),
            typeof(NotificationHandlerRegistration));

        var result = new StateMachineSourceGenerator().GetResult(compilation);
        var generatedCode = result.GeneratedTrees.Single().GetRoot().ToFullString();

        Assert.Contains("global::NOF.Abstraction.TypeResolver.Register(typeof(__OrderStateMachine_OrderCreatedNotification_Handler));", generatedCode);
        Assert.Contains("global::NOF.Abstraction.TypeResolver.Register(typeof(global::App.OrderCreatedNotification));", generatedCode);
        Assert.Contains("services.GetOrAddSingleton<global::NOF.Application.NotificationHandlerRegistry>().Add(new global::NOF.Application.NotificationHandlerRegistration(typeof(__OrderStateMachine_OrderCreatedNotification_Handler), typeof(global::App.OrderCreatedNotification)));", generatedCode);
    }
}
