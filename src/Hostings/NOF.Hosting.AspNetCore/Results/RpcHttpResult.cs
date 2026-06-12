using Microsoft.AspNetCore.Http;
using NOF.Abstraction;
using NOF.Contract;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting.AspNetCore;

internal sealed class RpcHttpResult(
    NOF.Contract.IResult rpcResult,
    IReadOnlyDictionary<string, string?> metadatas) : Microsoft.AspNetCore.Http.IResult
{
    private readonly NOF.Contract.IResult _rpcResult = rpcResult ?? throw new ArgumentNullException(nameof(rpcResult));
    private readonly IReadOnlyDictionary<string, string?> _metadatas = metadatas ?? throw new ArgumentNullException(nameof(metadatas));

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Transport response bodies are framework-controlled runtime payloads.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Transport response bodies are framework-controlled runtime payloads.")]
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ApplyHeaders(httpContext.Response, HttpTransportMetadata.GetHeaders(_metadatas));
        httpContext.Response.Headers[NOFAbstractionConstants.Transport.Headers.RpcSuccess] = bool.TrueString;
        httpContext.Response.StatusCode = ResolveStatusCode(_rpcResult, _metadatas);

        if (ShouldSkipBody(httpContext.Response.StatusCode))
        {
            return;
        }

        await httpContext.Response.WriteAsJsonAsync(
            _rpcResult,
            _rpcResult.GetType(),
            cancellationToken: httpContext.RequestAborted).ConfigureAwait(false);
    }

    internal static int ResolveStatusCode(NOF.Contract.IResult result, IReadOnlyDictionary<string, string?> metadatas)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(metadatas);
        if (HttpTransportMetadata.TryGetStatusCode(metadatas, out var statusCode))
        {
            return statusCode;
        }

        return result.IsSuccess
            ? StatusCodes.Status200OK
            : StatusCodes.Status500InternalServerError;
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
internal sealed class RpcStreamingHttpResult<TItem>(
    StreamingResult<TItem> rpcResult,
    IReadOnlyDictionary<string, string?> metadatas) : Microsoft.AspNetCore.Http.IResult
{
    private readonly StreamingResult<TItem> _rpcResult = rpcResult ?? throw new ArgumentNullException(nameof(rpcResult));
    private readonly IReadOnlyDictionary<string, string?> _metadatas = metadatas ?? throw new ArgumentNullException(nameof(metadatas));

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!_rpcResult.IsSuccess)
        {
            return new RpcHttpResult(_rpcResult, _metadatas).ExecuteAsync(httpContext);
        }

        RpcHttpResult.ApplyHeaders(httpContext.Response, HttpTransportMetadata.GetHeaders(_metadatas));
        httpContext.Response.Headers[NOFAbstractionConstants.Transport.Headers.RpcSuccess] = bool.TrueString;
        httpContext.Response.StatusCode = RpcHttpResult.ResolveStatusCode(_rpcResult, _metadatas);
        return TypedResults.ServerSentEvents(_rpcResult.Value!).ExecuteAsync(httpContext);
    }
}
