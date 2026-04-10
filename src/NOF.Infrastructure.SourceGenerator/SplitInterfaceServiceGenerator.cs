using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace NOF.Infrastructure.SourceGenerator;

[Generator]
public sealed class SplitInterfaceServiceGenerator : IIncrementalGenerator
{
    private const string AddSplitInterfaceServiceMethodName = "AddSplitInterfaceService";
    private const string ISplitedInterfaceFqn = "NOF.Application.ISplitedInterface<TService>";
    private const string IRpcServiceFqn = "NOF.Contract.IRpcService";

    private const string GeneratedNamespace = "NOF.Infrastructure.Generated";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var invocations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsPotentialInvocation(node),
                static (ctx, ct) => GetInvocation(ctx, ct))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!);

        context.RegisterSourceOutput(invocations.Collect(), static (spc, items) => Generate(spc, items));
    }

    private static bool IsPotentialInvocation(SyntaxNode node)
        => node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: AddSplitInterfaceServiceMethodName
            }
        };

    private static InvocationInfo? GetInvocation(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.Node is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name: GenericNameSyntax genericName
                }
            } invocation)
        {
            return null;
        }

        if (genericName.TypeArgumentList.Arguments.Count != 2)
        {
            return null;
        }

        if (context.SemanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation op)
        {
            return null;
        }

        var targetMethod = op.TargetMethod.ReducedFrom ?? op.TargetMethod;
        if (!IsAddSplitInterfaceServiceMethod(targetMethod))
        {
            return null;
        }

        if (context.SemanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[0], cancellationToken).Type is not INamedTypeSymbol serviceType)
        {
            return null;
        }

        if (context.SemanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[1], cancellationToken).Type is not INamedTypeSymbol splitedType)
        {
            return null;
        }

        if (!IsRpcServiceInterface(serviceType))
        {
            return null;
        }

        if (!ImplementsISplitedInterfaceOfService(splitedType, serviceType))
        {
            return null;
        }

#pragma warning disable RSEXPERIMENTAL002
        var location = context.SemanticModel.GetInterceptableLocation(invocation);
#pragma warning restore RSEXPERIMENTAL002
        if (location is null)
        {
            return null;
        }

        return new InvocationInfo(location, splitedType, serviceType);
    }

    private static bool IsAddSplitInterfaceServiceMethod(IMethodSymbol method)
    {
        var definition = (method.ReducedFrom ?? method).OriginalDefinition;

        if (definition.Name != AddSplitInterfaceServiceMethodName)
        {
            return false;
        }

        if (definition.TypeParameters.Length != 2)
        {
            return false;
        }

        return definition.ReturnType.ToDisplayString() == "Microsoft.Extensions.DependencyInjection.IServiceCollection";
    }

    private static bool IsRpcServiceInterface(INamedTypeSymbol serviceType)
    {
        if (serviceType.TypeKind != TypeKind.Interface)
        {
            return false;
        }

        if (serviceType.ToDisplayString() == IRpcServiceFqn)
        {
            return true;
        }

        foreach (var i in serviceType.AllInterfaces)
        {
            if (i.ToDisplayString() == IRpcServiceFqn)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ImplementsISplitedInterfaceOfService(INamedTypeSymbol splitedType, INamedTypeSymbol serviceType)
    {
        foreach (var i in splitedType.AllInterfaces)
        {
            if (i.IsGenericType
                && i.OriginalDefinition.ToDisplayString() == ISplitedInterfaceFqn
                && i.TypeArguments.Length == 1
                && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], serviceType))
            {
                return true;
            }
        }

        return false;
    }

    private static void Generate(SourceProductionContext context, ImmutableArray<InvocationInfo> invocations)
    {
        if (invocations.IsDefaultOrEmpty)
        {
            return;
        }

        var groups = invocations
            .GroupBy(static x =>
                (Splited: x.SplitedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                 Service: x.ServiceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                StringTupleComparer.Ordinal)
            .Select(static g => new InvocationGroup(
                g.First().SplitedType,
                g.First().ServiceType,
                g.Select(x => x.Location).ToImmutableArray()))
            .ToList();

        if (groups.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS1591");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
        sb.AppendLine();
        sb.AppendLine("namespace System.Runtime.CompilerServices");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.Diagnostics.Conditional(\"DEBUG\")]");
        sb.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]");
        sb.AppendLine("    file sealed class InterceptsLocationAttribute : global::System.Attribute");
        sb.AppendLine("    {");
        sb.AppendLine("        public InterceptsLocationAttribute(int version, string data)");
        sb.AppendLine("        {");
        sb.AppendLine("            _ = version;");
        sb.AppendLine("            _ = data;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"namespace {GeneratedNamespace}");
        sb.AppendLine("{");

        // Emit implementation classes first (one per group)
        foreach (var g in groups)
        {
            EmitImplementationClass(sb, g.SplitedType, g.ServiceType);
        }

        sb.AppendLine("    internal static class SplitInterfaceServiceInterceptors");
        sb.AppendLine("    {");
        foreach (var g in groups)
        {
            EmitAddSplitInterfaceInterceptor(sb, g.SplitedType, g.ServiceType, g.Locations);
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("SplitInterfaceServiceInterceptors.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void EmitAddSplitInterfaceInterceptor(
        StringBuilder sb,
        INamedTypeSymbol splitedType,
        INamedTypeSymbol serviceType,
        ImmutableArray<InterceptableLocation> locations)
    {
        foreach (var loc in locations)
        {
            sb.AppendLine($"        [global::System.Runtime.CompilerServices.InterceptsLocation({loc.Version}, \"{EscapeString(loc.Data)}\")]");
        }

        var methodName = $"AddSplitInterfaceService_{SanitizeIdentifier(serviceType.ToDisplayString())}_{SanitizeIdentifier(splitedType.ToDisplayString())}";
        sb.AppendLine($"        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection {methodName}(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("        {");

        var implName = GetImplementationTypeName(splitedType, serviceType);
        var serviceFqn = serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        sb.AppendLine("            global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.Replace(");
        sb.AppendLine("                services,");
        sb.AppendLine($"                global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Scoped<{serviceFqn}, {implName}>());");
        sb.AppendLine("            return services;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void EmitImplementationClass(StringBuilder sb, INamedTypeSymbol splitedType, INamedTypeSymbol serviceType)
    {
        var serviceFqn = serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var splitedFqn = splitedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var implName = GetImplementationTypeName(splitedType, serviceType);

        sb.AppendLine($"    internal sealed class {implName} : {serviceFqn}");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly global::NOF.Hosting.IOutboundPipelineExecutor _outboundPipeline;");
        sb.AppendLine("        private readonly global::NOF.Hosting.IExecutionContext _executionContext;");
        sb.AppendLine("        private readonly global::System.IServiceProvider _serviceProvider;");
        sb.AppendLine();
        sb.AppendLine($"        public {implName}(global::NOF.Hosting.IOutboundPipelineExecutor outboundPipeline, global::NOF.Hosting.IExecutionContext executionContext, global::System.IServiceProvider serviceProvider)");
        sb.AppendLine("        {");
        sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(outboundPipeline);");
        sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(executionContext);");
        sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(serviceProvider);");
        sb.AppendLine("            _outboundPipeline = outboundPipeline;");
        sb.AppendLine("            _executionContext = executionContext;");
        sb.AppendLine("            _serviceProvider = serviceProvider;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // helper: request + result
        sb.AppendLine("        private async global::System.Threading.Tasks.Task<TResult> ExecuteRpcAsync<TRequest, TResult>(");
        sb.AppendLine("            TRequest request,");
        sb.AppendLine("            global::System.Reflection.MethodInfo methodInfo,");
        sb.AppendLine("            global::System.Type handlerType,");
        sb.AppendLine("            global::System.Func<global::System.IServiceProvider, TRequest, global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task<TResult>> terminal,");
        sb.AppendLine("            global::System.Threading.CancellationToken cancellationToken)");
        sb.AppendLine("        {");
        sb.AppendLine("            var outboundContext = new global::NOF.Hosting.OutboundContext");
        sb.AppendLine("            {");
        sb.AppendLine("                Message = request,");
        sb.AppendLine("                Services = _serviceProvider");
        sb.AppendLine("            };");
        sb.AppendLine();
        sb.AppendLine("            TResult? result = default;");
        sb.AppendLine();
        sb.AppendLine("            await _outboundPipeline.ExecuteAsync(outboundContext, async ct =>");
        sb.AppendLine("            {");
        sb.AppendLine("                await global::NOF.Infrastructure.InboundHandlerInvoker.ExecuteRpcAsync(");
        sb.AppendLine("                    _serviceProvider,");
        sb.AppendLine("                    request,");
        sb.AppendLine("                    methodInfo,");
        sb.AppendLine("                    _executionContext,");
        sb.AppendLine("                    context =>");
        sb.AppendLine("                    {");
        sb.AppendLine("                        context.Metadatas[\"HandlerType\"] = handlerType;");
        sb.AppendLine("                    },");
        sb.AppendLine("                    async (sp, ct2) =>");
        sb.AppendLine("                    {");
        sb.AppendLine("                        result = await terminal(sp, request, ct2).ConfigureAwait(false);");
        sb.AppendLine("                    },");
        sb.AppendLine("                    ct).ConfigureAwait(false);");
        sb.AppendLine("            }, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine("            return result!;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // helper: no request
        sb.AppendLine("        private async global::System.Threading.Tasks.Task<TResult> ExecuteRpcAsync<TResult>(");
        sb.AppendLine("            global::System.Reflection.MethodInfo methodInfo,");
        sb.AppendLine("            global::System.Type handlerType,");
        sb.AppendLine("            global::System.Func<global::System.IServiceProvider, global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task<TResult>> terminal,");
        sb.AppendLine("            global::System.Threading.CancellationToken cancellationToken)");
        sb.AppendLine("        {");
        sb.AppendLine("            var outboundContext = new global::NOF.Hosting.OutboundContext");
        sb.AppendLine("            {");
        sb.AppendLine("                Message = null,");
        sb.AppendLine("                Services = _serviceProvider");
        sb.AppendLine("            };");
        sb.AppendLine();
        sb.AppendLine("            TResult? result = default;");
        sb.AppendLine();
        sb.AppendLine("            await _outboundPipeline.ExecuteAsync(outboundContext, async ct =>");
        sb.AppendLine("            {");
        sb.AppendLine("                await global::NOF.Infrastructure.InboundHandlerInvoker.ExecuteRpcAsync(");
        sb.AppendLine("                    _serviceProvider,");
        sb.AppendLine("                    null,");
        sb.AppendLine("                    methodInfo,");
        sb.AppendLine("                    _executionContext,");
        sb.AppendLine("                    context =>");
        sb.AppendLine("                    {");
        sb.AppendLine("                        context.Metadatas[\"HandlerType\"] = handlerType;");
        sb.AppendLine("                    },");
        sb.AppendLine("                    async (sp, ct2) =>");
        sb.AppendLine("                    {");
        sb.AppendLine("                        result = await terminal(sp, ct2).ConfigureAwait(false);");
        sb.AppendLine("                    },");
        sb.AppendLine("                    ct).ConfigureAwait(false);");
        sb.AppendLine("            }, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine("            return result!;");
        sb.AppendLine("        }");
        sb.AppendLine();

        var methods = GetServiceMethods(serviceType);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in methods)
        {
            var opName = GetOperationName(method.Name);
            var nestedInterfaceName = opName;
            if (!usedNames.Add(nestedInterfaceName))
            {
                var i = 2;
                while (!usedNames.Add($"{nestedInterfaceName}_{i}"))
                {
                    i++;
                }
                nestedInterfaceName = $"{nestedInterfaceName}_{i}";
            }

            EmitServiceMethod(sb, splitedFqn, nestedInterfaceName, serviceType, method);
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static List<IMethodSymbol> GetServiceMethods(INamedTypeSymbol iface)
    {
        return iface.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared)
            .ToList();
    }

    private static void EmitServiceMethod(
        StringBuilder sb,
        string splitedFqn,
        string nestedInterfaceName,
        INamedTypeSymbol serviceType,
        IMethodSymbol method)
    {
        if (method.ReturnType is not INamedTypeSymbol { IsGenericType: true } taskType
            || taskType.Name != "Task"
            || taskType.TypeArguments.Length != 1
            || taskType.ContainingNamespace.ToDisplayString() != "System.Threading.Tasks")
        {
            return;
        }

        var resultType = taskType.TypeArguments[0];

        var returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var resultTypeFqn = resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var methodName = method.Name;

        // Default values are not required for interface implementation; omit them to avoid mismatches.
        var parametersDecl = string.Join(", ", method.Parameters.Select(p =>
            $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"));

        var hasCancellationToken = method.Parameters.Any(p => p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken");
        var cancellationTokenArg = hasCancellationToken
            ? method.Parameters.First(p => p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken").Name
            : "global::System.Threading.CancellationToken.None";

        // Identify "request" parameter as the first non-ct parameter, if any.
        var requestParam = method.Parameters.FirstOrDefault(p =>
            p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != "global::System.Threading.CancellationToken");

        var handlerTypeFqn = $"{splitedFqn}.{nestedInterfaceName}";

        sb.AppendLine("        /// <inheritdoc />");
        sb.AppendLine($"        public {returnType} {methodName}({parametersDecl})");
        sb.AppendLine("        {");

        // Build MethodInfo lookup on the service interface (so attributes on the interface method are visible)
        var paramTypeArray = method.Parameters.Length == 0
            ? "global::System.Type.EmptyTypes"
            : "new global::System.Type[] { " + string.Join(", ", method.Parameters.Select(p => $"typeof({p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})")) + " }";
        var serviceFqn = serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        sb.AppendLine($"            var methodInfo = typeof({serviceFqn}).GetMethod(\"{EscapeString(methodName)}\", {paramTypeArray})!;");
        sb.AppendLine($"            var handlerType = typeof({handlerTypeFqn});");
        sb.AppendLine();

        if (requestParam is not null)
        {
            var requestType = requestParam.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var requestName = requestParam.Name;

            sb.AppendLine($"            return ExecuteRpcAsync<{requestType}, {resultTypeFqn}>(");
            sb.AppendLine($"                {requestName},");
            sb.AppendLine("                methodInfo,");
            sb.AppendLine("                handlerType,");
            sb.AppendLine("                (sp, request, ct) =>");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var handler = sp.GetRequiredService<{handlerTypeFqn}>();");

            var callArgs = new List<string>();
            foreach (var p in method.Parameters)
            {
                if (p.Equals(requestParam, SymbolEqualityComparer.Default))
                {
                    callArgs.Add("request");
                }
                else if (p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken")
                {
                    callArgs.Add("ct");
                }
                else
                {
                    // Additional parameters are captured from the outer scope.
                    callArgs.Add(p.Name);
                }
            }
            sb.AppendLine($"                    return handler.{methodName}({string.Join(", ", callArgs)});");
            sb.AppendLine("                },");
            sb.AppendLine($"                {cancellationTokenArg});");
        }
        else
        {
            sb.AppendLine($"            return ExecuteRpcAsync<{resultTypeFqn}>(");
            sb.AppendLine("                methodInfo,");
            sb.AppendLine("                handlerType,");
            sb.AppendLine("                (sp, ct) =>");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var handler = sp.GetRequiredService<{handlerTypeFqn}>();");

            var callArgs = new List<string>();
            foreach (var p in method.Parameters)
            {
                if (p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken")
                {
                    callArgs.Add("ct");
                }
                else
                {
                    callArgs.Add(p.Name);
                }
            }
            sb.AppendLine($"                    return handler.{methodName}({string.Join(", ", callArgs)});");
            sb.AppendLine("                },");
            sb.AppendLine($"                {cancellationTokenArg});");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static string GetOperationName(string methodName)
        => methodName.EndsWith("Async", StringComparison.Ordinal)
            ? methodName.Substring(0, methodName.Length - 5)
            : methodName;

    private static string GetImplementationTypeName(INamedTypeSymbol splitedType, INamedTypeSymbol serviceType)
        => $"__{SanitizeIdentifier(serviceType.ToDisplayString())}_{SanitizeIdentifier(splitedType.ToDisplayString())}_SplitInterfaceService__";

    private static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "_";
        }

        var sb = new StringBuilder(value.Length + 1);
        if (!char.IsLetter(value[0]) && value[0] != '_')
        {
            sb.Append('_');
        }

        foreach (var ch in value)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        return sb.ToString();
    }

    private static string EscapeString(string? value)
        => value is null ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private sealed class InvocationInfo
    {
        public InvocationInfo(InterceptableLocation location, INamedTypeSymbol splitedType, INamedTypeSymbol serviceType)
        {
            Location = location;
            SplitedType = splitedType;
            ServiceType = serviceType;
        }

        public InterceptableLocation Location { get; }
        public INamedTypeSymbol SplitedType { get; }
        public INamedTypeSymbol ServiceType { get; }
    }

    private sealed class InvocationGroup
    {
        public InvocationGroup(
            INamedTypeSymbol splitedType,
            INamedTypeSymbol serviceType,
            ImmutableArray<InterceptableLocation> locations)
        {
            SplitedType = splitedType;
            ServiceType = serviceType;
            Locations = locations;
        }

        public INamedTypeSymbol SplitedType { get; }
        public INamedTypeSymbol ServiceType { get; }
        public ImmutableArray<InterceptableLocation> Locations { get; }
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string Splited, string Service)>
    {
        public static readonly StringTupleComparer Ordinal = new();

        public bool Equals((string Splited, string Service) x, (string Splited, string Service) y)
            => string.Equals(x.Splited, y.Splited, StringComparison.Ordinal)
               && string.Equals(x.Service, y.Service, StringComparison.Ordinal);

        public int GetHashCode((string Splited, string Service) obj)
        {
            unchecked
            {
                var h1 = obj.Splited is null ? 0 : StringComparer.Ordinal.GetHashCode(obj.Splited);
                var h2 = obj.Service is null ? 0 : StringComparer.Ordinal.GetHashCode(obj.Service);
                return (h1 * 397) ^ h2;
            }
        }
    }
}
