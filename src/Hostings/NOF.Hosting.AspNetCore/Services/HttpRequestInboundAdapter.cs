using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NOF.Abstraction;
using NOF.Contract;
using NOF.Infrastructure;
using System.Diagnostics;
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

    public async Task<NOF.Contract.IResult?> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TRpcService>(
        HttpContext httpContext,
        string operationName,
        object request,
        CancellationToken cancellationToken)
        where TRpcService : class, IRpcService
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(request);
        var resolution = invocationResolver.Resolve<TRpcService>(operationName);

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
            if (IsAllowed(header.Key))
            {
                headers[header.Key] = header.Value.ToString();
            }
        }

        if (!headers.ContainsKey(NOFAbstractionConstants.Transport.Headers.MessageId))
        {
            headers[NOFAbstractionConstants.Transport.Headers.MessageId] = Guid.NewGuid().ToString();
        }

        var currentActivity = Activity.Current;
        if (currentActivity is not null)
        {
            headers.TryAdd(NOFAbstractionConstants.Transport.Headers.TraceId, currentActivity.TraceId.ToString());
            headers.TryAdd(NOFAbstractionConstants.Transport.Headers.SpanId, currentActivity.SpanId.ToString());
        }

        return headers;
    }

    private bool IsAllowed(string headerName)
        => _httpHeaderOptions.AllowedHeaders.Any(pattern => headerName.MatchWildcard(pattern, StringComparison.OrdinalIgnoreCase));
}
