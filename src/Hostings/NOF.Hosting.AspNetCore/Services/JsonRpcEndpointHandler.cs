using Microsoft.AspNetCore.Http;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace NOF.Hosting.AspNetCore;

internal static class JsonRpcEndpointHandler
{
    private const string JsonRpcVersion = "2.0";

    [RequiresUnreferencedCode("JSON-RPC request binding and response writing use runtime JSON type metadata.")]
    [RequiresDynamicCode("JSON-RPC request binding and response writing use runtime JSON type metadata.")]
    public static Delegate Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type serviceType,
        IReadOnlyDictionary<string, RpcHandlerMapping> handlerMappings)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(handlerMappings);

        Task<Microsoft.AspNetCore.Http.IResult> Handler(
            HttpContext httpContext,
            HttpRequestInboundAdapter inboundAdapter,
            IObjectSerializer serializer,
            CancellationToken cancellationToken)
            => HandleAsync(
                httpContext,
                serviceType,
                handlerMappings,
                inboundAdapter,
                serializer,
                cancellationToken);

        return Handler;
    }

    private static async Task<Microsoft.AspNetCore.Http.IResult> HandleAsync(
        HttpContext httpContext,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type serviceType,
        IReadOnlyDictionary<string, RpcHandlerMapping> handlerMappings,
        HttpRequestInboundAdapter inboundAdapter,
        IObjectSerializer serializer,
        CancellationToken cancellationToken)
    {
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(httpContext.Request.Body, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return CreateError(null, -32700, "Parse error");
        }

        using (document)
        {
            var root = document.RootElement;
            if (!TryReadRequest(root, out var method, out var parameters, out var id))
            {
                return CreateError(TryReadId(root), -32600, "Invalid Request");
            }

            if (!handlerMappings.TryGetValue(method, out var handlerMapping))
            {
                return CreateError(id, -32601, "Method not found");
            }

            if (IsStreamingResult(handlerMapping.ReturnType))
            {
                return CreateError(id, -32601, "Streaming RPC operations are not supported by the JSON-RPC endpoint.");
            }

            object request;
            try
            {
                var parameterPayload = parameters.HasValue
                    ? Encoding.UTF8.GetBytes(parameters.Value.GetRawText())
                    : "{}"u8.ToArray();
                request = serializer.Deserialize(parameterPayload, handlerMapping.RequestType)
                    ?? throw new JsonException("Request params deserialized to null.");
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
            {
                return CreateError(id, -32602, "Invalid params");
            }

            RequestInboundContext execution;
            try
            {
                execution = await inboundAdapter.InvokeAsync(
                    httpContext,
                    serviceType,
                    method,
                    request,
                    handlerMapping,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return CreateError(id, -32603, "Internal error");
            }

            if (execution.Response is null)
            {
                return CreateError(id, -32603, "Internal error");
            }

            try
            {
                var payload = serializer.Serialize(execution.Response, execution.Response.GetType());
                return CreateResult(id, payload);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
            {
                return CreateError(id, -32603, "Internal error");
            }
        }
    }

    private static bool TryReadRequest(
        JsonElement root,
        out string method,
        out JsonElement? parameters,
        out JsonElement id)
    {
        method = string.Empty;
        parameters = null;
        id = default;

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("jsonrpc", out var version)
            || version.ValueKind != JsonValueKind.String
            || !string.Equals(version.GetString(), JsonRpcVersion, StringComparison.Ordinal)
            || !root.TryGetProperty("method", out var methodElement)
            || methodElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(methodElement.GetString())
            || !root.TryGetProperty("id", out id)
            || id.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.Null))
        {
            return false;
        }

        if (root.TryGetProperty("params", out var paramsElement))
        {
            if (paramsElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            parameters = paramsElement;
        }

        method = methodElement.GetString()!;
        return true;
    }

    private static JsonElement? TryReadId(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("id", out var id)
            && id.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.Null)
        {
            return id;
        }

        return null;
    }

    private static bool IsStreamingResult(Type returnType)
        => returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(StreamingResult<>);

    private static Microsoft.AspNetCore.Http.IResult CreateResult(JsonElement id, ReadOnlyMemory<byte> result)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteString("jsonrpc", JsonRpcVersion);
        writer.WritePropertyName("result");
        writer.WriteRawValue(result.Span, skipInputValidation: false);
        writer.WritePropertyName("id");
        id.WriteTo(writer);
        writer.WriteEndObject();
        writer.Flush();
        return Results.Bytes(buffer.WrittenSpan.ToArray(), "application/json; charset=utf-8");
    }

    private static Microsoft.AspNetCore.Http.IResult CreateError(JsonElement? id, int code, string message)
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
        writer.WritePropertyName("id");
        if (id.HasValue)
        {
            id.Value.WriteTo(writer);
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WriteEndObject();
        writer.Flush();
        return Results.Bytes(buffer.WrittenSpan.ToArray(), "application/json; charset=utf-8");
    }
}
