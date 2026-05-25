using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using NOF.Contract;
using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NOF.Hosting.AspNetCore;

public sealed class NofRpcHttpResultWrappingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<NofRpcHttpEndpointMetadata>() is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context).ConfigureAwait(false);

            if (context.Response.HasStarted || context.Response.StatusCode == StatusCodes.Status200OK)
            {
                await CopyResponseBodyAsync(buffer, originalBody, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            var wrappedResult = await CreateFailureResultAsync(context.Response.StatusCode, buffer, context.RequestAborted).ConfigureAwait(false);
            context.Response.Body = originalBody;
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.Headers.ContentLength = null;
            context.Response.ContentType = "application/json; charset=utf-8";
            await WriteWrappedResultAsync(context.Response, wrappedResult, context.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static async Task CopyResponseBodyAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        source.Position = 0;
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> CreateFailureResultAsync(int statusCode, Stream responseBody, CancellationToken cancellationToken)
    {
        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        var message = ReasonPhrases.GetReasonPhrase(statusCode);
        if (string.IsNullOrWhiteSpace(message))
        {
            message = $"HTTP request failed with status code {statusCode.ToString(CultureInfo.InvariantCulture)}.";
        }

        Dictionary<string, string>? extra = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            if (TryReadProblemDetails(body, out var problemMessage, out var problemExtra))
            {
                message = problemMessage;
                extra = problemExtra;
            }
            else
            {
                message = body;
            }
        }

        return Result.From(Result.Fail(statusCode.ToString(CultureInfo.InvariantCulture), message, extra));
    }

    private static async Task WriteWrappedResultAsync(HttpResponse response, Result result, CancellationToken cancellationToken)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("isSuccess", result.IsSuccess);
            writer.WriteString("errorCode", result.ErrorCode);
            writer.WriteString("message", result.Message);
            writer.WritePropertyName("extra");
            if (result.Extra is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartObject();
                foreach (var pair in result.Extra)
                {
                    writer.WriteString(pair.Key, pair.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        await response.BodyWriter.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
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
                    if (error.Value.ValueKind == JsonValueKind.Array)
                    {
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
