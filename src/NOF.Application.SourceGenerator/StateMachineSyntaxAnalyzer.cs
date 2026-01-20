using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NOF.Application.SourceGenerator;

public static class StateMachineSyntaxAnalyzer
{
    public static List<CorrelationConfig> ExtractCorrelationConfigurations(MethodDeclarationSyntax buildMethod, SemanticModel semanticModel)
    {
        var configs = new List<CorrelationConfig>();

        if (buildMethod.Body == null)
            return configs;

        // Find all Correlate<TNotification>(...) calls
        var correlateCalls = buildMethod.Body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => IsCorrelateCall(invocation, semanticModel));

        foreach (var call in correlateCalls)
        {
            var config = ParseCorrelateCall(call, semanticModel);
            if (config != null)
                configs.Add(config);
        }

        return configs;
    }

    public static List<StartConfig> ExtractStartConfigurations(MethodDeclarationSyntax buildMethod, SemanticModel semanticModel)
    {
        var configs = new List<StartConfig>();

        if (buildMethod.Body == null)
            return configs;

        // Find all StartWhen<TNotification>(...) calls
        var startWhenCalls = buildMethod.Body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => IsStartWhenCall(invocation, semanticModel));

        foreach (var call in startWhenCalls)
        {
            var config = ParseStartWhenCall(call, semanticModel);
            if (config != null)
                configs.Add(config);
        }

        return configs;
    }

    public static List<TransferConfig> ExtractTransferConfigurations(MethodDeclarationSyntax buildMethod, SemanticModel semanticModel)
    {
        var configs = new List<TransferConfig>();

        if (buildMethod.Body == null)
            return configs;

        // Find all On(state) calls
        var onCalls = buildMethod.Body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => IsOnCall(invocation, semanticModel));

        foreach (var onCall in onCalls)
        {
            var config = ParseOnCall(onCall, semanticModel);
            if (config != null)
                configs.Add(config);
        }

        return configs;
    }

    private static bool IsCorrelateCall(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;
            if (methodName == "Correlate")
            {
                var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                return symbolInfo.Symbol?.ContainingType?.Name == "IStateMachineBuilder";
            }
        }
        return false;
    }

    private static bool IsStartWhenCall(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;
            if (methodName == "StartWhen")
            {
                var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                return symbolInfo.Symbol?.ContainingType?.Name == "IStateMachineBuilder";
            }
        }
        return false;
    }

    private static bool IsOnCall(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;
            if (methodName == "On")
            {
                var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                return symbolInfo.Symbol?.ContainingType?.Name == "IStateMachineBuilder";
            }
        }
        return false;
    }

    private static CorrelationConfig? ParseCorrelateCall(InvocationExpressionSyntax call, SemanticModel semanticModel)
    {
        // Correlate<TNotification>(selector)
        if (call.ArgumentList?.Arguments.Count != 1)
            return null;

        // For generic method calls, we need to check the parent expression
        var typeArgument = call.Expression is MemberAccessExpressionSyntax memberAccess &&
                           memberAccess.Name is GenericNameSyntax genericName
                           ? genericName.TypeArgumentList?.Arguments.FirstOrDefault()
                           : null;

        if (typeArgument == null)
            return null;

        var notificationType = semanticModel.GetTypeInfo(typeArgument).Type?.ToDisplayString();
        if (notificationType == null)
            return null;

        var selectorArg = call.ArgumentList.Arguments[0];
        var selectorExpression = selectorArg.Expression?.ToString();

        return new CorrelationConfig
        {
            NotificationType = notificationType,
            Expression = selectorExpression ?? "string.Empty"
        };
    }

    private static StartConfig? ParseStartWhenCall(InvocationExpressionSyntax call, SemanticModel semanticModel)
    {
        // StartWhen<TNotification>(initialState, contextFactory)
        if (call.ArgumentList?.Arguments.Count != 2)
            return null;

        // For generic method calls, we need to check the parent expression
        var typeArgument = call.Expression is MemberAccessExpressionSyntax memberAccess &&
                           memberAccess.Name is GenericNameSyntax genericName
                           ? genericName.TypeArgumentList?.Arguments.FirstOrDefault()
                           : null;

        if (typeArgument == null)
            return null;

        var notificationType = semanticModel.GetTypeInfo(typeArgument).Type?.ToDisplayString();
        if (notificationType == null)
            return null;

        var stateArg = call.ArgumentList.Arguments[0];
        var stateExpression = stateArg.Expression?.ToString();
        if (stateExpression == null)
            return null;

        // Extract state name (e.g., "SampleState.Processing" -> "Processing")
        var stateName = stateExpression.Split('.').LastOrDefault();

        var contextFactoryArg = call.ArgumentList.Arguments[1];
        var contextFactoryExpression = contextFactoryArg.Expression?.ToString();

        // Parse context factory to extract property assignments
        var contextProperties = ParseContextFactory(contextFactoryExpression);

        // Find chained method calls (ExecuteAsync, SendCommandAsync, etc.)
        var actions = ParseChainedActions(call);

        return new StartConfig
        {
            NotificationType = notificationType,
            State = stateName ?? "Unknown",
            ContextProperties = contextProperties,
            Actions = actions
        };
    }

    private static TransferConfig? ParseOnCall(InvocationExpressionSyntax onCall, SemanticModel semanticModel)
    {
        // On(state).When<TNotification>()...
        if (onCall.ArgumentList?.Arguments.Count != 1)
            return null;

        var stateArg = onCall.ArgumentList.Arguments[0];
        var stateExpression = stateArg.Expression?.ToString();
        var stateName = stateExpression?.Split('.').LastOrDefault();

        if (stateName == null)
            return null;

        var notificationConfigs = new List<NotificationConfig>();

        // Find chained When() calls
        var whenCalls = FindChainedWhenCalls(onCall);
        foreach (var whenCall in whenCalls)
        {
            var notificationConfig = ParseWhenCall(whenCall, semanticModel);
            if (notificationConfig != null)
                notificationConfigs.Add(notificationConfig);
        }

        return new TransferConfig
        {
            State = stateName,
            NotificationConfigs = notificationConfigs
        };
    }

    private static List<InvocationExpressionSyntax> FindChainedWhenCalls(InvocationExpressionSyntax onCall)
    {
        var whenCalls = new List<InvocationExpressionSyntax>();

        // Look for method chaining: On(state).When<TNotification>().ExecuteAsync(...).TransitionTo(...)
        var current = onCall.Parent as InvocationExpressionSyntax;

        while (current != null)
        {
            if (current.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "When")
            {
                whenCalls.Add(current);
            }
            current = current.Parent as InvocationExpressionSyntax;
        }

        return whenCalls;
    }

    private static NotificationConfig? ParseWhenCall(InvocationExpressionSyntax whenCall, SemanticModel semanticModel)
    {
        // For generic method calls, we need to check the parent expression
        var typeArgument = whenCall.Expression is MemberAccessExpressionSyntax memberAccess &&
                           memberAccess.Name is GenericNameSyntax genericName
                           ? genericName.TypeArgumentList?.Arguments.FirstOrDefault()
                           : null;

        if (typeArgument == null)
            return null;

        var notificationType = semanticModel.GetTypeInfo(typeArgument).Type?.ToDisplayString();
        if (notificationType == null)
            return null;

        var actions = new List<string>();
        string? targetState = null;

        // Find chained method calls after When()
        var current = whenCall.Parent as InvocationExpressionSyntax;
        while (current != null)
        {
            if (current.Expression is MemberAccessExpressionSyntax accessExpression)
            {
                var methodName = accessExpression.Name.Identifier.Text;

                if (methodName == "ExecuteAsync" || methodName == "SendCommandAsync" || methodName == "Modify")
                {
                    var actionExpression = current.ArgumentList?.Arguments.FirstOrDefault()?.Expression?.ToString();
                    if (actionExpression != null)
                        actions.Add($"await {actionExpression};");
                }
                else if (methodName == "TransitionTo")
                {
                    var stateArg = current.ArgumentList?.Arguments.FirstOrDefault()?.Expression?.ToString();
                    if (stateArg != null)
                        targetState = stateArg.Split('.').LastOrDefault();
                }
            }
            current = current.Parent as InvocationExpressionSyntax;
        }

        return new NotificationConfig
        {
            NotificationType = notificationType,
            TargetState = targetState,
            Actions = actions
        };
    }

    private static List<ContextProperty> ParseContextFactory(string? contextFactoryExpression)
    {
        var properties = new List<ContextProperty>();

        if (string.IsNullOrEmpty(contextFactoryExpression))
            return properties;

        // Simple parsing for expressions like: n => new SampleStateMachineContext { TaskId = n.TaskId, StartOn = DateTime.UtcNow }
        if (!string.IsNullOrEmpty(contextFactoryExpression) && contextFactoryExpression.Contains("new") && contextFactoryExpression.Contains("{"))
        {
            var startIndex = contextFactoryExpression.IndexOf('{');
            var endIndex = contextFactoryExpression.LastIndexOf('}');

            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                var propertiesSection = contextFactoryExpression.Substring(startIndex + 1, endIndex - startIndex - 1);
                var propertyAssignments = propertiesSection.Split(',');

                foreach (var assignment in propertyAssignments)
                {
                    var trimmed = assignment.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && trimmed.Contains('='))
                    {
                        var parts = trimmed.Split(new[] { '=' }, 2);
                        var propertyName = parts[0].Trim();
                        var propertyValue = parts[1].Trim();

                        properties.Add(new ContextProperty
                        {
                            Name = propertyName,
                            Value = propertyValue
                        });
                    }
                }
            }
        }

        return properties;
    }

    private static List<string> ParseChainedActions(InvocationExpressionSyntax startWhenCall)
    {
        var actions = new List<string>();

        // Find chained method calls after StartWhen
        var current = startWhenCall.Parent as InvocationExpressionSyntax;
        while (current != null)
        {
            if (current.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var methodName = memberAccess.Name.Identifier.Text;

                if (methodName == "ExecuteAsync" || methodName == "SendCommandAsync")
                {
                    var actionExpression = current.ArgumentList?.Arguments.FirstOrDefault()?.Expression?.ToString();
                    if (actionExpression != null)
                        actions.Add($"await {actionExpression};");
                }
            }
            current = current.Parent as InvocationExpressionSyntax;
        }

        return actions;
    }
}
