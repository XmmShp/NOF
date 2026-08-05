using Microsoft.AspNetCore.Http;
using NOF.Contract;
using NOF.Infrastructure;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

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

internal sealed class JsonRpcStreamingHttpResult<TItem>(
    StreamingResult<TItem> rpcResult,
    ReadOnlyMemory<byte> requestId,
    IObjectSerializer serializer) : Microsoft.AspNetCore.Http.IResult
{
    private const string JsonRpcVersion = "2.0";
    private static readonly ReadOnlyMemory<byte> _dataPrefix = "data: "u8.ToArray();
    private static readonly ReadOnlyMemory<byte> _eventSeparator = "\n\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> _errorEvent = "event: error\n"u8.ToArray();
    private readonly StreamingResult<TItem> _rpcResult = rpcResult ?? throw new ArgumentNullException(nameof(rpcResult));
    private readonly ReadOnlyMemory<byte> _requestId = requestId;
    private readonly IObjectSerializer _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";

        try
        {
            await foreach (var item in _rpcResult.Value!.WithCancellation(httpContext.RequestAborted).ConfigureAwait(false))
            {
                var itemPayload = item is null
                    ? "null"u8.ToArray()
                    : _serializer.Serialize(item, typeof(TItem));
                var envelope = CreateResultEnvelope(itemPayload);
                await WriteEventAsync(httpContext, envelope, isError: false).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            var envelope = CreateErrorEnvelope(-32603, "Internal error");
            await WriteEventAsync(httpContext, envelope, isError: true).ConfigureAwait(false);
        }
    }

    private byte[] CreateResultEnvelope(ReadOnlyMemory<byte> result)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteString("jsonrpc", JsonRpcVersion);
        writer.WritePropertyName("result");
        writer.WriteRawValue(result.Span, skipInputValidation: false);
        WriteRequestId(writer);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private byte[] CreateErrorEnvelope(int code, string message)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteString("jsonrpc", JsonRpcVersion);
        writer.WritePropertyName("error");
        writer.WriteStartObject();
        writer.WriteNumber("code", code);
        writer.WriteString("message", message);
        writer.WriteEndObject();
        WriteRequestId(writer);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private void WriteRequestId(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("id");
        writer.WriteRawValue(_requestId.Span, skipInputValidation: false);
    }

    private static async Task WriteEventAsync(HttpContext httpContext, ReadOnlyMemory<byte> envelope, bool isError)
    {
        if (isError)
        {
            await httpContext.Response.Body.WriteAsync(_errorEvent, httpContext.RequestAborted).ConfigureAwait(false);
        }

        await httpContext.Response.Body.WriteAsync(_dataPrefix, httpContext.RequestAborted).ConfigureAwait(false);
        await httpContext.Response.Body.WriteAsync(envelope, httpContext.RequestAborted).ConfigureAwait(false);
        await httpContext.Response.Body.WriteAsync(_eventSeparator, httpContext.RequestAborted).ConfigureAwait(false);
        await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted).ConfigureAwait(false);
    }
}
