using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using System.Diagnostics.CodeAnalysis;

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

    public async Task<RequestInboundContext> InvokeAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TRpcService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TRequest>(
        HttpContext httpContext,
        string operationName,
        TRequest request,
        CancellationToken cancellationToken)
        where TRpcService : class, IRpcService
    {
        var resolution = invocationResolver.Resolve<TRpcService>(operationName);
        return await InvokeAsync(
            httpContext,
            typeof(TRpcService),
            operationName,
            request!,
            resolution.HandlerMapping,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<RequestInboundContext> InvokeAsync(
        HttpContext httpContext,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type serviceType,
        string operationName,
        object request,
        RpcHandlerMapping handlerMapping,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(handlerMapping);

        var headers = CreateInboundHeaders(httpContext);
        var execution = await inboundPipeline.ExecuteAsync(
            request,
            handlerMapping.HandlerType,
            handlerMapping.ReturnType,
            serviceType,
            operationName,
            headers,
            cancellationToken).ConfigureAwait(false);
        ApplyResponseHeaders(httpContext, execution.ResponseHeaders);
        return execution;
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

    private static void ApplyResponseHeaders(HttpContext httpContext, IEnumerable<KeyValuePair<string, string?>> headers)
    {
        foreach (var (name, value) in headers)
        {
            if (string.IsNullOrWhiteSpace(name) || value is null)
            {
                continue;
            }

            if (string.Equals(name, NOFInfrastructureConstants.Transport.Headers.HttpStatusCode, StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, out var statusCode))
                {
                    httpContext.Items[NOFInfrastructureConstants.Transport.Headers.HttpStatusCode] = statusCode;
                }

                continue;
            }

            httpContext.Response.Headers[name] = value;
        }
    }
}
