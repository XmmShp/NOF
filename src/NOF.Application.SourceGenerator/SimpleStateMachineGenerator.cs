using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF.Application.SourceGenerator;

[Generator]
public class SimpleStateMachineGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList.Types.Count: > 0 },
                transform: static (ctx, _) => GetStateMachineDefinition(ctx))
            .Where(static m => m is not null);

        var compilationAndClasses = context.CompilationProvider.Combine(provider.Collect());
        context.RegisterSourceOutput(compilationAndClasses, GenerateStateMachines);
    }

    private static StateMachineDefinitionInfo? GetStateMachineDefinition(GeneratorSyntaxContext ctx)
    {
        var classDeclaration = (ClassDeclarationSyntax)ctx.Node;
        var semanticModel = ctx.SemanticModel;
        var symbol = semanticModel.GetDeclaredSymbol(classDeclaration);

        if (symbol == null)
            return null;

        // Check if class implements IStateMachineDefinition<TState, TContext>
        var stateMachineDefinitionInterface = symbol.AllInterfaces
            .FirstOrDefault(i => i.Name == "IStateMachineDefinition" && i.TypeParameters.Length == 2);

        if (stateMachineDefinitionInterface == null)
            return null;

        // Get type arguments
        var typeArgs = stateMachineDefinitionInterface.TypeArguments;
        if (typeArgs.Length != 2)
            return null;

        var stateType = typeArgs[0] as INamedTypeSymbol;
        var contextType = typeArgs[1] as INamedTypeSymbol;

        // Find the Build method
        var buildMethod = symbol.GetMembers("Build")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Parameters.Length == 1 &&
                                m.Parameters[0].Type.Name == "IStateMachineBuilder");

        if (buildMethod == null)
            return null;

        return new StateMachineDefinitionInfo
        {
            Symbol = symbol,
            StateType = stateType,
            ContextType = contextType,
            BuildMethod = buildMethod,
            ClassName = symbol.Name,
            Namespace = symbol.ContainingNamespace.ToDisplayString()
        };
    }

    private static void GenerateStateMachines(SourceProductionContext context,
        (Compilation Compilation, ImmutableArray<StateMachineDefinitionInfo?> StateMachines) source)
    {
        if (source.StateMachines.IsDefaultOrEmpty)
            return;

        var validStateMachines = source.StateMachines.Where(sm => sm != null).Cast<StateMachineDefinitionInfo>().ToList();

        foreach (var stateMachine in validStateMachines)
        {
            try
            {
                // Extract all information from the state machine definition
                var (notificationTypes, correlationConfigs, startConfigs, transferConfigs) = ExtractNotificationTypes(stateMachine, source.Compilation);
                var stateMachineLogic = ExtractStateMachineLogic(stateMachine, source.Compilation);

                // Generate a single handler that implements multiple INotificationHandler<T> interfaces
                var handlerSourceCode = GenerateCompleteNotificationHandler(stateMachine, notificationTypes, correlationConfigs, startConfigs, transferConfigs, stateMachineLogic);
                var fileName = $"{stateMachine.ClassName}NotificationHandler.g.cs";
                context.AddSource(fileName, SourceText.From(handlerSourceCode, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "NOFSG001",
                        "State Machine Generation Error",
                        $"Error generating notification handler for {stateMachine.ClassName}: {ex.Message}",
                        "NOF.SourceGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None));
            }
        }
    }

    private static (List<INamedTypeSymbol> notificationTypes, List<CorrelationConfig> correlationConfigs, List<StartConfig> startConfigs, List<TransferConfig> transferConfigs) ExtractNotificationTypes(StateMachineDefinitionInfo stateMachine, Compilation compilation)
    {
        var notificationTypes = new HashSet<INamedTypeSymbol>();
        var correlationConfigs = new List<CorrelationConfig>();
        var startConfigs = new List<StartConfig>();
        var transferConfigs = new List<TransferConfig>();

        // Get the Build method syntax
        var buildMethodSyntax = stateMachine.BuildMethod.DeclaringSyntaxReferences
            .FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;

        if (buildMethodSyntax?.Body == null)
            return (notificationTypes.ToList(), correlationConfigs, startConfigs, transferConfigs);

        var semanticModel = compilation.GetSemanticModel(buildMethodSyntax.SyntaxTree);

        // Find all method calls in the Build method
        var invocationExpressions = buildMethodSyntax.Body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocationExpressions)
        {
            // Check for Correlate<TNotification>, StartWhen<TNotification>, or When<TNotification>()
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var methodName = memberAccess.Name.Identifier.Text;

                if (methodName is "Correlate" or "StartWhen" or "When")
                {
                    // Get the generic type argument
                    if (memberAccess.Name is GenericNameSyntax genericName)
                    {
                        var typeArgument = genericName.TypeArgumentList.Arguments.FirstOrDefault();
                        if (typeArgument != null)
                        {
                            var typeSymbol = semanticModel.GetTypeInfo(typeArgument).Type as INamedTypeSymbol;
                            if (typeSymbol != null && typeSymbol.AllInterfaces.Any(i => i.Name == "INotification"))
                            {
                                notificationTypes.Add(typeSymbol);

                                // If this is a Correlate call, extract the correlation expression
                                if (methodName == "Correlate")
                                {
                                    var correlationExpression = ExtractCorrelationExpression(invocation, out var parameterName);
                                    if (correlationExpression != null)
                                    {
                                        correlationConfigs.Add(new CorrelationConfig
                                        {
                                            NotificationType = typeSymbol.ToDisplayString(),
                                            Expression = correlationExpression,
                                            ParameterName = parameterName ?? "notification"
                                        });
                                    }
                                }
                                else if (methodName == "StartWhen")
                                {
                                    // Extract StartWhen configuration
                                    var startConfig = ExtractStartConfiguration(invocation, semanticModel, typeSymbol);
                                    if (startConfig != null)
                                    {
                                        startConfigs.Add(startConfig);
                                    }
                                }
                                else if (methodName == "When")
                                {
                                    // Extract When configuration (transfer)
                                    var transferConfig = ExtractTransferConfiguration(invocation, semanticModel, typeSymbol);
                                    if (transferConfig != null)
                                    {
                                        transferConfigs.Add(transferConfig);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return (notificationTypes.ToList(), correlationConfigs, startConfigs, transferConfigs);
    }

    private static string? ExtractCorrelationExpression(InvocationExpressionSyntax correlateCall, out string? parameterName)
    {
        parameterName = null;
        
        // Get the argument passed to Correlate method
        if (correlateCall.ArgumentList?.Arguments.Count > 0)
        {
            var argument = correlateCall.ArgumentList.Arguments[0].Expression;
            
            // Check if it's a lambda expression
            if (argument is ParenthesizedLambdaExpressionSyntax parenthesizedLambda)
            {
                // Extract parameter name
                if (parenthesizedLambda.ParameterList.Parameters.Count > 0)
                {
                    parameterName = parenthesizedLambda.ParameterList.Parameters[0].Identifier.Text;
                }
                
                // Extract lambda body
                return parenthesizedLambda.Body?.ToString();
            }
            else if (argument is SimpleLambdaExpressionSyntax simpleLambda)
            {
                // Extract parameter name
                parameterName = simpleLambda.Parameter.Identifier.Text;
                
                // Extract lambda body
                return simpleLambda.Body?.ToString();
            }
            
            return argument?.ToString();
        }
        return null;
    }

    private static StartConfig? ExtractStartConfiguration(InvocationExpressionSyntax startWhenCall, SemanticModel semanticModel, INamedTypeSymbol notificationType)
    {
        // Extract the initial state and context factory from StartWhen call
        if (startWhenCall.ArgumentList?.Arguments.Count >= 2)
        {
            var stateArg = startWhenCall.ArgumentList.Arguments[0].Expression;
            var contextFactoryArg = startWhenCall.ArgumentList.Arguments[1].Expression;
            
            return new StartConfig
            {
                NotificationType = notificationType.ToDisplayString(),
                State = stateArg?.ToString() ?? "Unknown",
                ContextProperties = new List<ContextProperty>(),
                Actions = new List<string> { contextFactoryArg?.ToString() ?? "// No context factory" }
            };
        }
        return null;
    }

    private static TransferConfig? ExtractTransferConfiguration(InvocationExpressionSyntax whenCall, SemanticModel semanticModel, INamedTypeSymbol notificationType)
    {
        // Extract transfer configuration from When call
        // This is more complex as it involves chained calls like .When<TNotification>().ExecuteAsync(...)
        // For now, return a basic configuration
        return new TransferConfig
        {
            State = "Unknown",
            NotificationConfigs = new List<NotificationConfig>
            {
                new NotificationConfig
                {
                    NotificationType = notificationType.ToDisplayString(),
                    TargetState = "Unknown",
                    Actions = new List<string> { "// ExecuteAsync action to be extracted" }
                }
            }
        };
    }

    private static StateMachineLogic ExtractStateMachineLogic(StateMachineDefinitionInfo stateMachine, Compilation compilation)
    {
        var logic = new StateMachineLogic();

        // Get the Build method syntax
        var buildMethodSyntax = stateMachine.BuildMethod.DeclaringSyntaxReferences
            .FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;

        if (buildMethodSyntax?.Body == null)
            return logic;

        var semanticModel = compilation.GetSemanticModel(buildMethodSyntax.SyntaxTree);

        // Extract all methods, classes, and logic from the state machine definition

        if (stateMachine.Symbol.DeclaringSyntaxReferences
                .FirstOrDefault()?.GetSyntax() is ClassDeclarationSyntax classDeclaration)
        {
            // Extract all members (methods, properties, classes, etc.)
            foreach (var member in classDeclaration.Members)
            {
                switch (member)
                {
                    case MethodDeclarationSyntax method:
                        if (method.Identifier.Text != "Build") // Skip the Build method itself
                        {
                            logic.Members.Add(method.ToFullString());
                        }
                        break;
                    case PropertyDeclarationSyntax property:
                        logic.Members.Add(property.ToFullString());
                        break;
                    case FieldDeclarationSyntax field:
                        logic.Members.Add(field.ToFullString());
                        break;
                    case ClassDeclarationSyntax nestedClass:
                        logic.Members.Add(nestedClass.ToFullString());
                        break;
                    case ConstructorDeclarationSyntax constructor:
                        logic.Members.Add(constructor.ToFullString());
                        break;
                }
            }
        }

        return logic;
    }

    private static string GenerateCompleteNotificationHandler(StateMachineDefinitionInfo stateMachine, List<INamedTypeSymbol> notificationTypes, List<CorrelationConfig> correlationConfigs, List<StartConfig> startConfigs, List<TransferConfig> transferConfigs, StateMachineLogic stateMachineLogic)
    {
        var sb = new StringBuilder();
        var namespaceName = stateMachine.Namespace;
        var className = stateMachine.ClassName;
        var handlerClassName = $"{className}NotificationHandler";

        // Add file header
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#pragma warning disable CS1591");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Add usings
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();

        // Add namespace
        sb.AppendLine($"namespace {namespaceName}");
        sb.AppendLine("{");

        // Generate the notification handler class with multiple interface implementations
        sb.AppendLine($"    [EditorBrowsable(EditorBrowsableState.Never)]");
        sb.Append($"    public sealed class {handlerClassName} : INotificationHandler");

        // Add all the generic interface implementations
        for (int i = 0; i < notificationTypes.Count; i++)
        {
            var notificationType = notificationTypes[i].ToDisplayString();
            if (i == 0)
                sb.Append($"<{notificationType}>");
            else
                sb.Append($", INotificationHandler<{notificationType}>");
        }
        sb.AppendLine();
        sb.AppendLine("    {");

        // Include all the original state machine members (methods, properties, classes, etc.)
        foreach (var member in stateMachineLogic.Members)
        {
            sb.AppendLine($"    {member}");
        }

        // Constructor and fields for the handler
        sb.AppendLine($"        private readonly IStateMachineContextRepository _repository;");
        sb.AppendLine($"        private readonly IUnitOfWork _uow;");
        sb.AppendLine($"        private readonly IServiceProvider _serviceProvider;");
        sb.AppendLine();
        sb.AppendLine($"        public {handlerClassName}(");
        sb.AppendLine($"            IStateMachineContextRepository repository,");
        sb.AppendLine($"            IUnitOfWork uow,");
        sb.AppendLine($"            IServiceProvider serviceProvider)");
        sb.AppendLine("        {");
        sb.AppendLine($"            _repository = repository;");
        sb.AppendLine($"            _uow = uow;");
        sb.AppendLine($"            _serviceProvider = serviceProvider;");
        sb.AppendLine("        }");

        // Generate the generic GetStateMachineInfo method
        sb.AppendLine();
        sb.AppendLine("        private async Task<StateMachineInfo?> GetStateMachineInfo<TNotification>(Func<TNotification, string> correlator, TNotification notification)");
        sb.AppendLine("        {");
        sb.AppendLine("            var correlationId = correlator(notification);");
        sb.AppendLine("            ArgumentNullException.ThrowIfNullOrEmpty(correlationId);");
        sb.AppendLine("            return await _repository.FindAsync(correlationId, typeof(" + className + "));");
        sb.AppendLine("        }");

        // Generate the generic CreateStateMachineContext method
        sb.AppendLine();
        sb.AppendLine($"        private {stateMachine.ContextType?.ToDisplayString() ?? "TContext"} CreateStateMachineContext<TNotification>(Func<TNotification, {stateMachine.ContextType?.ToDisplayString() ?? "TContext"}> contextFactory, TNotification notification)");
        sb.AppendLine("        {");
        sb.AppendLine("            return contextFactory(notification);");
        sb.AppendLine("        }");

        // Generate HandleAsync method for each notification type
        foreach (var notificationType in notificationTypes)
        {
            var notificationTypeName = notificationType.ToDisplayString();
            var correlationConfig = correlationConfigs.FirstOrDefault(c => c.NotificationType == notificationTypeName);
            var startConfig = startConfigs.FirstOrDefault(c => c.NotificationType == notificationTypeName);
            var transferConfigsForNotification = transferConfigs.Where(c => c.NotificationConfigs.Any(nc => nc.NotificationType == notificationTypeName)).ToList();
            
            sb.AppendLine();
            sb.AppendLine($"        public async Task HandleAsync({notificationTypeName} notification, CancellationToken cancellationToken)");
            sb.AppendLine("        {");
            sb.AppendLine($"            // Generated handler for {className} processing {notificationTypeName}");
            
            if (correlationConfig != null)
            {
                sb.AppendLine($"            var stateMachineInfo = await GetStateMachineInfo({correlationConfig.ParameterName} => {correlationConfig.Expression}, notification);");
                
                // Check if this is a start configuration
                if (startConfig != null)
                {
                    sb.AppendLine("            if (stateMachineInfo == null)");
                    sb.AppendLine("            {");
                    sb.AppendLine("                // Start new state machine instance");
                    sb.AppendLine($"                var newContext = CreateStateMachineContext({startConfig.Actions.FirstOrDefault() ?? "n => new()"}, notification);");
                    sb.AppendLine($"                var correlationId = {correlationConfig.Expression.Replace(correlationConfig.ParameterName, "notification")};");
                    sb.AppendLine("                var newStateMachineInfo = StateMachineInfo.Create(");
                    sb.AppendLine("                    correlationId,");
                    sb.AppendLine($"                    typeof({className}),");
                    sb.AppendLine("                    newContext,");
                    sb.AppendLine($"                    Convert.ToInt32({startConfig.State}));");
                    sb.AppendLine("                _repository.Add(newStateMachineInfo);");
                    sb.AppendLine("                stateMachineInfo = newStateMachineInfo;");
                    sb.AppendLine("            }");
                }
                
                sb.AppendLine("            if (stateMachineInfo != null)");
                sb.AppendLine("            {");
                
                // Handle transfer configurations
                foreach (var transferConfig in transferConfigsForNotification)
                {
                    var notificationConfig = transferConfig.NotificationConfigs.FirstOrDefault(nc => nc.NotificationType == notificationTypeName);
                    if (notificationConfig != null)
                    {
                        sb.AppendLine($"                // Handle transfer to {notificationConfig.TargetState}");
                        sb.AppendLine("                // Execute actions from the original state machine definition");
                        sb.AppendLine("                // TODO: Call extracted methods from the original state machine");
                        sb.AppendLine("                // Update state if needed");
                        sb.AppendLine($"                // stateMachineInfo = stateMachineInfo with {{ State = {notificationConfig.TargetState} }};");
                        sb.AppendLine("                _repository.Update(stateMachineInfo);");
                    }
                }
                
                sb.AppendLine("            }");
                sb.AppendLine("            else");
                sb.AppendLine("            {");
                sb.AppendLine("                // No state machine context found and no start configuration");
                sb.AppendLine($"                throw new InvalidOperationException($\"No state machine instance found for correlation ID: {correlationConfig.Expression.Replace(correlationConfig.ParameterName, "notification")}\");");
                sb.AppendLine("            }");
            }
            else
            {
                sb.AppendLine("            // No correlation configuration found for this notification type");
                sb.AppendLine("            throw new InvalidOperationException(\"No correlation configuration found for this notification type\");");
            }
            
            sb.AppendLine();
            sb.AppendLine("            await Task.CompletedTask;");
            sb.AppendLine("        }");
        }

        // Close class and namespace
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
