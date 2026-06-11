using System.Globalization;
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
            return RpcResults.Success(payload!);
        }

        return await ReadFailureAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<Result> ReadFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        var statusCode = (int)response.StatusCode;
        var message = response.ReasonPhrase;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = $"HTTP request failed with status code {statusCode.ToString(CultureInfo.InvariantCulture)}.";
        }

        Dictionary<string, string>? extra = null;
        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(body))
        {
            if (TryReadProblemDetails(body, out var detailMessage, out var problemExtra))
            {
                message = detailMessage;
                extra = problemExtra;
            }
            else
            {
                message = body;
            }
        }

        return Result.From(Result.Fail(statusCode.ToString(CultureInfo.InvariantCulture), message!, extra));
    }

    public static async Task<RpcResult<T>> ReadFailureAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var failure = await ReadFailureAsync(response, cancellationToken).ConfigureAwait(false);
        return RpcResults.FromFailure<T>(failure);
    }

    private static bool TryReadProblemDetails(string body, out string message, out Dictionary<string, string>? extra)
    {
        message = string.Empty;
        extra = null;

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (document.RootElement.TryGetProperty("detail", out var detailProperty)
                && detailProperty.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(detailProperty.GetString()))
            {
                message = detailProperty.GetString()!;
            }
            else if (document.RootElement.TryGetProperty("title", out var titleProperty)
                     && titleProperty.ValueKind == JsonValueKind.String
                     && !string.IsNullOrWhiteSpace(titleProperty.GetString()))
            {
                message = titleProperty.GetString()!;
            }
            else if (document.RootElement.TryGetProperty("message", out var messageProperty)
                     && messageProperty.ValueKind == JsonValueKind.String
                     && !string.IsNullOrWhiteSpace(messageProperty.GetString()))
            {
                message = messageProperty.GetString()!;
            }

            if (document.RootElement.TryGetProperty("errors", out var errorsProperty)
                && errorsProperty.ValueKind == JsonValueKind.Object)
            {
                extra = [];
                foreach (var error in errorsProperty.EnumerateObject())
                {
                    if (error.Value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    var values = error.Value.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToArray();
                    if (values.Length > 0)
                    {
                        extra[error.Name] = string.Join(" | ", values);
                    }
                }

                if (extra.Count == 0)
                {
                    extra = null;
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    message = "One or more validation errors occurred.";
                }
            }

            return !string.IsNullOrWhiteSpace(message) || extra is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
