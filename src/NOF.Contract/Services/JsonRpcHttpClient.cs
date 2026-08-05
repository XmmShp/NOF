using System.Buffers;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Contract;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class JsonRpcHttpClient
{
    private const string JsonRpcVersion = "2.0";

    public static async Task<TResponse> SendAsync<
        TRequest,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TResponse>(
        HttpClient httpClient,
        string endpoint,
        string method,
        TRequest request,
        IEnumerable<KeyValuePair<string, string?>> headers,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken cancellationToken)
        where TResponse : IResult
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(requestTypeInfo);
        ArgumentNullException.ThrowIfNull(responseTypeInfo);

        var id = Guid.NewGuid().ToString("N");
        var requestPayload = CreateRequestPayload(id, method, request, requestTypeInfo);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(requestPayload)
        };
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8"
        };
        ApplyHeaders(httpRequest, headers);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return await HttpRpcTransportResultReader.ReadFailureAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        ValidateResponseEnvelope(root, id);

        if (root.TryGetProperty("result", out var resultElement))
        {
            return resultElement.Deserialize(responseTypeInfo)
                ?? throw new InvalidOperationException($"JSON-RPC response result for '{typeof(TResponse).FullName}' is null.");
        }

        if (root.TryGetProperty("error", out var errorElement)
            && errorElement.ValueKind == JsonValueKind.Object)
        {
            var code = errorElement.TryGetProperty("code", out var codeElement)
                ? codeElement.ToString()
                : "-32603";
            var message = errorElement.TryGetProperty("message", out var messageElement)
                && messageElement.ValueKind == JsonValueKind.String
                    ? messageElement.GetString() ?? "JSON-RPC error"
                    : "JSON-RPC error";
            return ResultProjection.RequireCompatible<TResponse>(Result.Fail(code, message));
        }

        throw new InvalidOperationException("JSON-RPC response must contain either a result or an error object.");
    }

    private static byte[] CreateRequestPayload<TRequest>(
        string id,
        string method,
        TRequest request,
        JsonTypeInfo<TRequest> requestTypeInfo)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteString("jsonrpc", JsonRpcVersion);
        writer.WriteString("method", method);
        writer.WritePropertyName("params");
        JsonSerializer.Serialize(writer, request, requestTypeInfo);
        writer.WriteString("id", id);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static void ApplyHeaders(HttpRequestMessage request, IEnumerable<KeyValuePair<string, string?>> headers)
    {
        foreach (var (name, value) in headers)
        {
            if (value is null)
            {
                continue;
            }

            request.Headers.Remove(name);
            request.Headers.TryAddWithoutValidation(name, value);
        }
    }

    private static void ValidateResponseEnvelope(JsonElement root, string expectedId)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("jsonrpc", out var version)
            || version.ValueKind != JsonValueKind.String
            || !string.Equals(version.GetString(), JsonRpcVersion, StringComparison.Ordinal)
            || !root.TryGetProperty("id", out var id)
            || id.ValueKind != JsonValueKind.String
            || !string.Equals(id.GetString(), expectedId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid JSON-RPC response envelope.");
        }
    }
}
