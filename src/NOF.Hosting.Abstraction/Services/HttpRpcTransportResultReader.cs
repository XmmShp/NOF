using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
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

        if (response.IsSuccessStatusCode)
        {
            var payload = await HttpContentJsonExtensions.ReadFromJsonAsync(response.Content, successTypeInfo, cancellationToken).ConfigureAwait(false);
            return RpcResults.Success(payload!, ReadStatusCode(response), ReadHeaders(response));
        }

        return await ReadFailureAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IRpcResult> ReadFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        var statusCode = (int)response.StatusCode;
        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return RpcResults.Fail(
            statusCode,
            string.IsNullOrWhiteSpace(body) ? null : body,
            ReadHeaders(response));
    }

    public static async Task<RpcResult<T>> ReadFailureAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var envelope = await ReadFailureAsync(response, cancellationToken).ConfigureAwait(false);
        return RpcResults.From<T>(envelope);
    }

    private static int ReadStatusCode(HttpResponseMessage response) => (int)response.StatusCode;

    private static IReadOnlyDictionary<string, string?> ReadHeaders(HttpResponseMessage response)
        => response.Headers
            .Concat(response.Content?.Headers.AsEnumerable() ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            .SelectMany(static header => header.Value.Select(value => new KeyValuePair<string, string?>(header.Key, value)))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);

}
