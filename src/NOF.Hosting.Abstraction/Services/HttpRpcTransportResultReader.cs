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
    public static async Task<(T Response, IReadOnlyDictionary<string, string?> Metadatas)> ReadAsync<T>(
        HttpResponseMessage response,
        JsonTypeInfo<T> successTypeInfo,
        CancellationToken cancellationToken)
        where T : IResult
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(successTypeInfo);

        var metadatas = ReadMetadatas(response);
        if (ReadTransportSuccess(response))
        {
            if (IsBodyEmpty(response))
            {
                return (ConvertResult<T>(ResultProjection.CreateSuccess(typeof(T))), metadatas);
            }

            try
            {
                var payload = await HttpContentJsonExtensions.ReadFromJsonAsync(response.Content, successTypeInfo, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"HTTP RPC response body for '{typeof(T).FullName}' is empty.");
                return (payload, metadatas);
            }
            catch (JsonException) when (!response.IsSuccessStatusCode)
            {
                var failure = await CreateTransportFailureAsync(response, cancellationToken).ConfigureAwait(false);
                return (ConvertResult<T>(failure), metadatas);
            }
            catch (NotSupportedException) when (!response.IsSuccessStatusCode)
            {
                var failure = await CreateTransportFailureAsync(response, cancellationToken).ConfigureAwait(false);
                return (ConvertResult<T>(failure), metadatas);
            }
        }

        return await ReadFailureAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<(T Response, IReadOnlyDictionary<string, string?> Metadatas)> ReadFailureAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : IResult
    {
        ArgumentNullException.ThrowIfNull(response);

        var metadatas = ReadMetadatas(response);
        var failure = await CreateTransportFailureAsync(response, cancellationToken).ConfigureAwait(false);
        return (ConvertResult<T>(failure), metadatas);
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

    private static async Task<string?> ReadTransportBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

    private static async Task<IResult> CreateTransportFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await ReadTransportBodyAsync(response, cancellationToken).ConfigureAwait(false);
        var message = body ?? response.ReasonPhrase ?? "HTTP RPC transport failure.";
        return Result.Fail(((int)response.StatusCode).ToString(), message);
    }

    private static T ConvertResult<T>(IResult result)
        where T : IResult
    {
        ArgumentNullException.ThrowIfNull(result);
        return ResultProjection.RequireCompatible<T>(result);
    }
}
