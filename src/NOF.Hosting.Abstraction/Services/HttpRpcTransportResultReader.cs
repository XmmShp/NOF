using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using NOF.Abstraction;
using NOF.Contract;

namespace NOF.Hosting;

public static class HttpRpcTransportResultReader
{
    public static async Task<RpcResult<T>> ReadAsync<T>(
        HttpResponseMessage response,
        JsonTypeInfo<T> successTypeInfo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(successTypeInfo);

        if (ReadTransportSuccess(response))
        {
            var metadatas = ReadMetadatas(response);
            if (IsBodyEmpty(response))
            {
                return RpcResults.Response<T>(true, metadatas: metadatas);
            }

            if (!response.IsSuccessStatusCode)
            {
                var fallbackBody = await ReadTransportBodyAsync(response, cancellationToken).ConfigureAwait(false);
                return RpcResults.Response<T>(true, fallbackBody, metadatas);
            }

            try
            {
                var payload = await HttpContentJsonExtensions.ReadFromJsonAsync(response.Content, successTypeInfo, cancellationToken).ConfigureAwait(false);
                return RpcResults.Response<T>(true, payload, metadatas);
            }
            catch (JsonException)
            {
                var fallbackBody = await ReadTransportBodyAsync(response, cancellationToken).ConfigureAwait(false);
                return RpcResults.Response<T>(true, fallbackBody, metadatas);
            }
            catch (NotSupportedException)
            {
                var fallbackBody = await ReadTransportBodyAsync(response, cancellationToken).ConfigureAwait(false);
                return RpcResults.Response<T>(true, fallbackBody, metadatas);
            }
        }

        return await ReadFailureAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IRpcResult> ReadFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        _ = cancellationToken;
        return RpcResults.Fail(ReadMetadatas(response));
    }

    public static async Task<RpcResult<T>> ReadFailureAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var envelope = await ReadFailureAsync(response, cancellationToken).ConfigureAwait(false);
        return RpcResults.From<T>(envelope);
    }

    private static bool ReadTransportSuccess(HttpResponseMessage response)
    {
        if (TryReadTransportSuccess(response.Headers, out var success))
        {
            return success;
        }

        if (response.Content is not null && TryReadTransportSuccess(response.Content.Headers, out success))
        {
            return success;
        }

        return response.IsSuccessStatusCode;
    }

    private static bool TryReadTransportSuccess(HttpHeaders headers, out bool success)
    {
        success = default;
        if (headers.TryGetValues(NOFAbstractionConstants.Transport.Headers.RpcSuccess, out var values)
            && bool.TryParse(values.FirstOrDefault(), out success))
        {
            return true;
        }

        return false;
    }

    private static bool IsBodyEmpty(HttpResponseMessage response)
        => response.Content is null
            || response.Content.Headers.ContentLength == 0;

    private static IReadOnlyDictionary<string, string?> ReadMetadatas(HttpResponseMessage response)
        => HttpTransportMetadata.Create((int)response.StatusCode, ReadHeaders(response));

    private static IReadOnlyDictionary<string, string?> ReadHeaders(HttpResponseMessage response)
        => response.Headers
            .Concat(response.Content?.Headers.AsEnumerable() ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            .Where(static header => !string.Equals(
                header.Key,
                NOFAbstractionConstants.Transport.Headers.RpcSuccess,
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(static header => header.Value.Select(value => new KeyValuePair<string, string?>(header.Key, value)))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    private static async Task<object?> ReadTransportBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return null;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mediaType, "text/json", StringComparison.OrdinalIgnoreCase)
            || mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) == true)
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

}
