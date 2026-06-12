using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Infrastructure;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Hosting.AspNetCore;

/// <summary>
/// Adapts an inbound HTTP request into the NOF request inbound pipeline
/// without first going through the request outbound pipeline.
/// </summary>
public sealed class HttpRequestInboundAdapter(
    RpcServerInvocationResolver invocationResolver,
    RequestInboundPipelineExecutor inboundPipeline,
    IOptions<HttpHeaderOutboundOptions> httpHeaderOptions)
{
    private readonly HttpHeaderOutboundOptions _httpHeaderOptions = httpHeaderOptions.Value;

    public async Task<IRpcResult?> InvokeAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TRpcService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TRequest>(
        HttpContext httpContext,
        string operationName,
        TRequest request,
        CancellationToken cancellationToken)
        where TRpcService : class, IRpcService
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(request);
        var resolution = invocationResolver.Resolve<TRpcService>(operationName);

        BindHeaderProperties(httpContext, request);
        var headers = CreateInboundHeaders(httpContext);
        return await inboundPipeline.ExecuteAsync(
            request,
            resolution.HandlerMapping.HandlerType,
            typeof(TRpcService),
            operationName,
            headers,
            cancellationToken).ConfigureAwait(false);
    }

    private Dictionary<string, string?> CreateInboundHeaders(HttpContext httpContext)
    {
        var headers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in httpContext.Request.Headers)
        {
            if (!string.Equals(header.Key, NOFAbstractionConstants.Transport.Headers.TraceParent, StringComparison.OrdinalIgnoreCase)
                && IsAllowed(header.Key))
            {
                headers[header.Key] = header.Value.ToString();
            }
        }

        if (!headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.MessageId))
        {
            headers[NOFAbstractionConstants.Transport.Headers.MessageId] = Guid.NewGuid().ToString();
        }

        return headers;
    }

    private bool IsAllowed(string headerName)
        => _httpHeaderOptions.AllowedHeaders.Any(pattern => headerName.MatchWildcard(pattern, StringComparison.OrdinalIgnoreCase));

    private static void BindHeaderProperties<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TRequest>(
        HttpContext httpContext,
        TRequest request)
    {
        foreach (var property in typeof(TRequest).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var attribute = property.GetCustomAttribute<FromHeaderAttribute>(inherit: true);
            if (attribute is null || !property.CanWrite)
            {
                continue;
            }

            if (!httpContext.Request.Headers.TryGetValue(attribute.HeaderName, out var headerValues))
            {
                continue;
            }

            var value = headerValues.ToString();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            property.SetValue(request, TransportStringValueConverter.Convert(value, property.PropertyType));
        }
    }
}
