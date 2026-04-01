using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace NOF.Hosting.AspNetCore.SourceGenerator;

internal static class ExposeToHttpEndpointHelpers
{
	public const string HttpEndpointAttributeFqn = "NOF.Contract.HttpEndpointAttribute";
	public const string RpcServiceInterfaceFqn = "NOF.Contract.IRpcService";

	public static bool IsRpcServiceInterface(INamedTypeSymbol symbol)
		=> symbol.TypeKind == TypeKind.Interface
		   && (symbol.ToDisplayString() == RpcServiceInterfaceFqn
			   || symbol.AllInterfaces.Any(i => i.ToDisplayString() == RpcServiceInterfaceFqn));

	public static bool IsCancellationToken(ITypeSymbol typeSymbol)
		=> typeSymbol.ToDisplayString() == "System.Threading.CancellationToken";

	public static bool TryGetRequestParameter(IMethodSymbol method, out IParameterSymbol? parameter)
	{
		var candidates = method.Parameters.Where(p => !IsCancellationToken(p.Type)).ToList();
		if (candidates.Count == 0)
		{
			parameter = null;
			return true;
		}
		if (candidates.Count == 1)
		{
			parameter = candidates[0];
			return true;
		}

		parameter = null!;
		return false;
	}

	public static bool TryGetServiceReturnInfo(IMethodSymbol method, out ServiceReturnInfo returnInfo)
	{
		if (method.ReturnType.ToDisplayString() == "System.Threading.Tasks.Task")
		{
			returnInfo = new ServiceReturnInfo(ServiceReturnKind.Task, null);
			return true;
		}

		if (method.ReturnType is INamedTypeSymbol { IsGenericType: true } generic &&
			generic.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.Task<TResult>")
		{
			returnInfo = new ServiceReturnInfo(ServiceReturnKind.TaskOfT, generic.TypeArguments[0]);
			return true;
		}

		returnInfo = default;
		return false;
	}

	public static List<IPropertySymbol> GetAllPublicProperties(INamedTypeSymbol typeSymbol)
	{
		var properties = new List<IPropertySymbol>();
		var seen = new HashSet<string>();
		var current = typeSymbol;
		while (current != null)
		{
			foreach (var member in current.GetMembers())
			{
				if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false, IsIndexer: false, GetMethod: not null } prop
					&& seen.Add(prop.Name))
				{
					properties.Add(prop);
				}
			}

			current = current.BaseType;
		}

		return properties;
	}

	public static List<string> ExtractRouteParameters(string route)
	{
		var result = new List<string>();
		var matches = Regex.Matches(route, @"\{(\w+)\}");
		foreach (Match match in matches)
		{
			result.Add(match.Groups[1].Value);
		}

		return result;
	}

	public static EndpointInfo ExtractEndpointInfo(ServiceMethodInfo method)
	{
		var methodAttr = method.Method.GetAttributes()
			.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == HttpEndpointAttributeFqn);

		var httpMethod = methodAttr is null
			? HttpVerb.Post
			: (HttpVerb)methodAttr.ConstructorArguments[0].Value!;

		var route = methodAttr is null
			? method.OperationName
			: methodAttr.ConstructorArguments.Length > 1
				? methodAttr.ConstructorArguments[1].Value as string
				: null;

		route = route?.TrimEnd('/');
		route ??= method.OperationName;

		var displayName = TryGetCtorString(method.Method.GetAttributes(), "NOF.Contract.EndpointNameAttribute");

		var description = TryGetCtorString(method.Method.GetAttributes(), "NOF.Contract.EndpointDescriptionAttribute");

		var summary = TryGetCtorString(method.Method.GetAttributes(), "NOF.Contract.SummaryAttribute");

		var tags = method.Method.GetAttributes()
			.Where(a => a.AttributeClass?.ToDisplayString() == "System.ComponentModel.CategoryAttribute")
			.Select(a => a.ConstructorArguments.FirstOrDefault().Value as string)
			.Where(v => v != null)
			.Cast<string>()
			.ToArray();

		return new EndpointInfo
		{
			ServiceType = method.Method.ContainingType,
			ServiceMethodName = method.Method.Name,
			ServiceHasCancellationToken = method.Method.Parameters.Any(p => IsCancellationToken(p.Type)),
			RequestType = method.RequestType,
			ReturnInfo = method.ReturnInfo,
			Method = httpMethod,
			Route = route,
			OperationName = method.OperationName,
			DisplayName = displayName,
			Description = description,
			Summary = summary,
			Tags = tags
		};
	}

	private static string? TryGetCtorString(ImmutableArray<AttributeData> attributes, string attributeFqn)
		=> attributes
			.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == attributeFqn)
			?.ConstructorArguments.FirstOrDefault().Value as string;
}

internal sealed class ServiceMethodInfo
{
	public IMethodSymbol Method { get; set; } = null!;
	public INamedTypeSymbol? RequestType { get; set; }
	public ServiceReturnInfo ReturnInfo { get; set; } = new(ServiceReturnKind.Task, null);
	public string OperationName { get; set; } = string.Empty;
}

internal class EndpointInfo
{
	public INamedTypeSymbol ServiceType { get; set; } = null!;
	public string ServiceMethodName { get; set; } = string.Empty;
	public bool ServiceHasCancellationToken { get; set; }
	public INamedTypeSymbol? RequestType { get; set; }
	public ServiceReturnInfo ReturnInfo { get; set; } = new(ServiceReturnKind.Task, null);
	public HttpVerb Method { get; set; }
	public string Route { get; set; } = string.Empty;
	public string OperationName { get; set; } = string.Empty;
	public string? DisplayName { get; set; }
	public string? Description { get; set; }
	public string? Summary { get; set; }
	public string[] Tags { get; set; } = [];
}

internal enum HttpVerb
{
	Get,
	Post,
	Put,
	Delete,
	Patch
}

internal enum ServiceReturnKind
{
	Task,
	TaskOfT
}

internal readonly struct ServiceReturnInfo
{
	public ServiceReturnInfo(ServiceReturnKind kind, ITypeSymbol? valueType)
	{
		Kind = kind;
		ValueType = valueType;
	}

	public ServiceReturnKind Kind { get; }
	public ITypeSymbol? ValueType { get; }
}

