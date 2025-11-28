using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace NOF;

/// <summary>
/// 源生成器：为标记了 [HttpEndpoint] 且继承自 RequestHandler 的类生成 Minimal API 注册代码
/// </summary>
[Generator]
public class HttpEndpointMinimalApiGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (n, _) => n is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, _) => GetClassDeclarationWithHttpEndpoint(ctx))
            .Where(static m => m is not null);

        var compilationAndClasses = context.CompilationProvider.Combine(provider.Collect());
        context.RegisterSourceOutput(compilationAndClasses, Execute);
    }

    private static ClassDeclarationSyntax? GetClassDeclarationWithHttpEndpoint(GeneratorSyntaxContext ctx)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.Node;
        return classDecl.AttributeLists
            .Any(attrList => attrList.Attributes
                .Select(attr => ctx.SemanticModel.GetSymbolInfo(attr).Symbol as IMethodSymbol)
                .Any(symbol => symbol?.ContainingType?.Name == "HttpEndpointAttribute"
                               && (symbol.ContainingType.ToDisplayString() == "NOF.HttpEndpointAttribute"
                                   || symbol.ContainingType.ContainingNamespace.ToDisplayString() == "NOF")))
            ? classDecl
            : null;
    }

    private static void Execute(SourceProductionContext spc, (Compilation Compilation, ImmutableArray<ClassDeclarationSyntax?> Classes) source)
    {
        if (source.Classes.IsDefaultOrEmpty)
            return;

        var allEndpoints = new List<(INamedTypeSymbol ClassSymbol, EndpointInfo Info)>();

        foreach (var classDecl in source.Classes.Distinct())
        {
            if (classDecl is null)
            {
                continue;
            }
            spc.CancellationToken.ThrowIfCancellationRequested();

            var model = source.Compilation.GetSemanticModel(classDecl.SyntaxTree);
            var classSymbol = model.GetDeclaredSymbol(classDecl);
            if (classSymbol is null)
                continue;

            // 验证是否继承 RequestHandler
            if (!TryGetRequestHandlerInfo(classSymbol, out var requestType))
            {
                var loc = classDecl.Identifier.GetLocation();
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "NOF001",
                        title: "Invalid Handler Base Class",
                        messageFormat: "Class '{0}' must inherit from RequestHandler<T> or RequestHandler<T, R>",
                        category: "Usage",
                        defaultSeverity: DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    location: loc,
                    classSymbol.Name));
                continue;
            }

            var endpoints = GetHttpEndpointAttributes(classSymbol, requestType!);
            allEndpoints.AddRange(endpoints.Select(ep => (classSymbol, ep)));
        }

        if (allEndpoints.Count <= 0)
        {
            return;
        }

        var code = GenerateUnifiedRegistration(allEndpoints);
        spc.AddSource("RequestEndpoints.g.cs", SourceText.From(code, Encoding.UTF8));
    }

    private static bool TryGetRequestHandlerInfo(
        INamedTypeSymbol classSymbol,
        out ITypeSymbol? requestType)
    {
        requestType = null;

        var baseType = classSymbol.BaseType;
        while (baseType != null)
        {
            if (baseType.Name == "RequestHandler" &&
                baseType.ContainingNamespace?.ToDisplayString() == "NOF")
            {
                var args = baseType.TypeArguments;
                if (args.Length >= 1)
                {
                    requestType = args[0];
                    return true;
                }
            }
            baseType = baseType.BaseType;
        }

        return false;
    }

    private static List<EndpointInfo> GetHttpEndpointAttributes(
        INamedTypeSymbol classSymbol,
        ITypeSymbol requestType)
    {
        var list = new List<EndpointInfo>();

        foreach (var attr in classSymbol.GetAttributes()
                     .Where(attr => attr.AttributeClass?.Name == "HttpEndpointAttribute")
                     .Where(attr => attr.ConstructorArguments.Length >= 1))
        {
            if (attr.ConstructorArguments[0].Value is not int methodInt)
            {
                continue;
            }

            var method = (HttpVerb)methodInt;
            var group = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1].Value as string : null;
            var route = attr.ConstructorArguments.Length > 2 ? attr.ConstructorArguments[2].Value as string : null;

            // 如果 route 为空，使用 Request 类型名（去掉 "Request" 后缀）
            if (string.IsNullOrEmpty(route))
            {
                var name = requestType.Name;
                route = name.EndsWith("Request") ? name.Substring(0, name.Length - 7) : name;
                route = char.ToLowerInvariant(route[0]) + route.Substring(1);
            }

            // 解析命名参数
            string? permission = null;
            var allowAnonymous = false;
            foreach (var named in attr.NamedArguments)
            {
                switch (named.Key)
                {
                    case "Permission":
                        permission = named.Value.Value as string;
                        break;
                    case "AllowAnonymous":
                        allowAnonymous = named.Value.Value is true;
                        break;
                }
            }

            list.Add(new EndpointInfo
            {
                RequestType = requestType,
                Method = method,
                Group = group,
                Route = route ?? "endpoint",
                Permission = permission,
                AllowAnonymous = allowAnonymous
            });
        }

        return list;
    }

    private static string GenerateUnifiedRegistration(List<(INamedTypeSymbol ClassSymbol, EndpointInfo Info)> allEndpoints)
    {
        const string targetNamespace = "NOF";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.AspNetCore.Builder;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using MassTransit;");
        sb.AppendLine();

        sb.AppendLine($"namespace {targetNamespace};");
        sb.AppendLine();
        sb.AppendLine("public static class RequestEndpoints");
        sb.AppendLine("{");
        sb.AppendLine("    public static void MapRequestEndpoints(this WebApplication app)");
        sb.AppendLine("    {");

        foreach (var (_, ep) in allEndpoints)
        {
            var path = string.IsNullOrEmpty(ep.Group)
                ? $"/{ep.Route}"
                : $"/{ep.Group}/{ep.Route}";

            var httpMethod = ep.Method switch
            {
                HttpVerb.Get => "Get",
                HttpVerb.Post => "Post",
                HttpVerb.Put => "Put",
                HttpVerb.Delete => "Delete",
                HttpVerb.Patch => "Patch",
                _ => "Post"
            };

            var reqType = ep.RequestType.ToDisplayString();

            sb.AppendLine($"        app.Map{httpMethod}(\"{path}\", async ({reqType} request, IScopedMediator mediator) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            return await mediator.SendRequest(request);");
            sb.AppendLine("        })");

            if (ep.AllowAnonymous)
            {
                sb.AppendLine("        .AllowAnonymous()");
            }
            else if (!string.IsNullOrEmpty(ep.Permission))
            {
                sb.AppendLine($"        .RequirePermission(\"{ep.Permission}\")");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private sealed class EndpointInfo
    {
        public ITypeSymbol RequestType { get; set; } = null!;
        public HttpVerb Method { get; set; }
        public string? Group { get; set; }
        public string Route { get; set; } = null!;
        public string? Permission { get; set; }
        public bool AllowAnonymous { get; set; }
    }
}

internal enum HttpVerb
{
    Get,
    Post,
    Put,
    Delete,
    Patch
}