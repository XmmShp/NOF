using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using NOF.Contract;

namespace NOF.Hosting;

public static class HttpRpcTransportResultReader
{
    public static async Task<T> ReadAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>(
        HttpResponseMessage response,
        JsonTypeInfo<T> successTypeInfo,
        CancellationToken cancellationToken)
        where T : IResult
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(successTypeInfo);

        if (response.IsSuccessStatusCode)
        {
            if (IsBodyEmpty(response))
            {
                return ConvertResult<T>(ResultProjection.CreateSuccess(typeof(T)));
            }

            var payload = await HttpContentJsonExtensions.ReadFromJsonAsync(response.Content, successTypeInfo, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"HTTP RPC response body for '{typeof(T).FullName}' is empty.");
            return payload;
        }

        return await ReadFailureAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<T> ReadFailureAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : IResult
    {
        ArgumentNullException.ThrowIfNull(response);

        var failure = await CreateTransportFailureAsync(response, cancellationToken).ConfigureAwait(false);
        return ConvertResult<T>(failure);
    }

    private static bool IsBodyEmpty(HttpResponseMessage response)
        => response.Content is null
            || response.Content.Headers.ContentLength == 0;

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

    private static T ConvertResult<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>(IResult result)
        where T : IResult
    {
        ArgumentNullException.ThrowIfNull(result);
        return ResultProjection.RequireCompatible<T>(result);
    }
}
