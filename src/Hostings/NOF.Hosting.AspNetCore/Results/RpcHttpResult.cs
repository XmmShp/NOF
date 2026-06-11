using Microsoft.AspNetCore.Http;
using NOF.Contract;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting.AspNetCore;

[RequiresUnreferencedCode("HTTP response writing may require runtime JSON serialization for transport bodies.")]
[RequiresDynamicCode("HTTP response writing may require runtime JSON serialization for transport bodies.")]
internal sealed class RpcHttpResult(IRpcResult rpcResult) : Microsoft.AspNetCore.Http.IResult
{
    private readonly IRpcResult _rpcResult = rpcResult ?? throw new ArgumentNullException(nameof(rpcResult));

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ApplyHeaders(httpContext.Response, _rpcResult.Headers);
        httpContext.Response.StatusCode = ResolveStatusCode(_rpcResult);

        if (ShouldSkipBody(httpContext.Response.StatusCode) || _rpcResult.Body is null)
        {
            return;
        }

        if (_rpcResult.Body is string textBody)
        {
            if (string.IsNullOrWhiteSpace(httpContext.Response.ContentType))
            {
                httpContext.Response.ContentType = "text/plain; charset=utf-8";
            }

            await httpContext.Response.WriteAsync(textBody, httpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        await httpContext.Response.WriteAsJsonAsync(
            _rpcResult.Body,
            _rpcResult.Body.GetType(),
            cancellationToken: httpContext.RequestAborted).ConfigureAwait(false);
    }

    internal static int ResolveStatusCode(IRpcResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.StatusCode ?? StatusCodes.Status200OK;
    }

    internal static void ApplyHeaders(HttpResponse response, IReadOnlyDictionary<string, string?> headers)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(headers);

        foreach (var (headerName, value) in headers)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                response.Headers.Remove(headerName);
                continue;
            }

            response.Headers[headerName] = value;
        }
    }

    private static bool ShouldSkipBody(int statusCode)
        => statusCode is StatusCodes.Status204NoContent
            or StatusCodes.Status205ResetContent
            or StatusCodes.Status304NotModified
            or StatusCodes.Status301MovedPermanently
            or StatusCodes.Status302Found
            or StatusCodes.Status303SeeOther
            or StatusCodes.Status307TemporaryRedirect
            or StatusCodes.Status308PermanentRedirect;
}

[RequiresUnreferencedCode("Streaming HTTP response writing may require runtime JSON serialization for transport bodies.")]
[RequiresDynamicCode("Streaming HTTP response writing may require runtime JSON serialization for transport bodies.")]
internal sealed class RpcStreamingHttpResult<TItem>(RpcResult<StreamingResult<TItem>> rpcResult) : Microsoft.AspNetCore.Http.IResult
{
    private readonly RpcResult<StreamingResult<TItem>> _rpcResult = rpcResult ?? throw new ArgumentNullException(nameof(rpcResult));

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!_rpcResult.IsSuccess)
        {
            return new RpcHttpResult(_rpcResult).ExecuteAsync(httpContext);
        }

        var streamingResult = _rpcResult.Value
            ?? throw new InvalidOperationException($"HTTP RPC endpoint returned '{typeof(RpcResult<StreamingResult<TItem>>).FullName}' without a value.");
        if (!streamingResult.IsSuccess)
        {
            return new RpcHttpResult(_rpcResult).ExecuteAsync(httpContext);
        }

        RpcHttpResult.ApplyHeaders(httpContext.Response, _rpcResult.Headers);
        httpContext.Response.StatusCode = RpcHttpResult.ResolveStatusCode(_rpcResult);
        return TypedResults.ServerSentEvents(streamingResult.Value!).ExecuteAsync(httpContext);
    }
}
