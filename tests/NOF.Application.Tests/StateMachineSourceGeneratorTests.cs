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

        Assert.Contains("services.ReplaceOrAdd(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Transient(typeof(__OrderStateMachine_OrderCreatedNotification_Handler), typeof(__OrderStateMachine_OrderCreatedNotification_Handler)));", generatedCode);
        Assert.Contains("services.Add(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof(____OrderStateMachine_OrderCreatedNotification_HandlerNotificationInboundInvoker), typeof(____OrderStateMachine_OrderCreatedNotification_HandlerNotificationInboundInvoker)));", generatedCode);
        Assert.Contains("services.GetOrAddSingleton<global::NOF.Application.NotificationHandlerRegistry>().Add(new global::NOF.Application.NotificationHandlerRegistration(typeof(__OrderStateMachine_OrderCreatedNotification_Handler), typeof(global::App.OrderCreatedNotification), typeof(____OrderStateMachine_OrderCreatedNotification_HandlerNotificationInboundInvoker)));", generatedCode);
        Assert.Contains("internal sealed class ____OrderStateMachine_OrderCreatedNotification_HandlerNotificationInboundInvoker : global::NOF.Application.INotificationInboundHandlerInvoker", generatedCode);
        Assert.Contains("public string MessageTypeName => \"App.OrderCreatedNotification\";", generatedCode);
        Assert.Contains("return deserialize(payload, typeof(global::App.OrderCreatedNotification))", generatedCode);
        Assert.Contains("handler.HandleAsync((global::App.OrderCreatedNotification)message, context, cancellationToken)", generatedCode);
        Assert.DoesNotContain("IMessagePayloadBinder", generatedCode);
        Assert.DoesNotContain("IMessageTypeBinder", generatedCode);
        Assert.DoesNotContain("TypeResolver.Register", generatedCode);
    }
}
