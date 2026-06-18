using Microsoft.AspNetCore.Http;
using NOF.Contract;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting.AspNetCore;

internal sealed class RpcHttpResult(
    Contract.IResult rpcResult,
    int statusCode = StatusCodes.Status200OK) : Microsoft.AspNetCore.Http.IResult
{
    private readonly Contract.IResult _rpcResult = rpcResult ?? throw new ArgumentNullException(nameof(rpcResult));

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Transport response bodies are framework-controlled runtime payloads.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Transport response bodies are framework-controlled runtime payloads.")]
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(
            _rpcResult,
            _rpcResult.GetType(),
            cancellationToken: httpContext.RequestAborted).ConfigureAwait(false);
    }
}

[RequiresUnreferencedCode("Streaming HTTP response writing may require runtime JSON serialization for transport bodies.")]
[RequiresDynamicCode("Streaming HTTP response writing may require runtime JSON serialization for transport bodies.")]
internal sealed class RpcStreamingHttpResult<TItem>(
    StreamingResult<TItem> rpcResult) : Microsoft.AspNetCore.Http.IResult
{
    private readonly StreamingResult<TItem> _rpcResult = rpcResult ?? throw new ArgumentNullException(nameof(rpcResult));

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!_rpcResult.IsSuccess)
        {
            return new RpcHttpResult(_rpcResult).ExecuteAsync(httpContext);
        }

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        return TypedResults.ServerSentEvents(_rpcResult.Value!).ExecuteAsync(httpContext);
    }
}
