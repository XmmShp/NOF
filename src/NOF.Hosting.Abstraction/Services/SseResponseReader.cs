using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NOF.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class SseResponseReader
{
    public static async IAsyncEnumerable<T> ReadAsync<T>(
        HttpResponseMessage response,
        JsonTypeInfo<T> jsonTypeInfo,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            var dataBuilder = new StringBuilder();

            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (line.Length == 0)
                {
                    if (dataBuilder.Length == 0)
                    {
                        continue;
                    }

                    yield return DeserializeItem(dataBuilder.ToString(), jsonTypeInfo);
                    dataBuilder.Clear();
                    continue;
                }

                if (line.StartsWith(':'))
                {
                    continue;
                }

                if (!line.StartsWith("data:", StringComparison.Ordinal))
                {
                    continue;
                }

                var data = line.Length > 5 && line[5] == ' '
                    ? line.Substring(6)
                    : line.Substring(5);
                if (dataBuilder.Length > 0)
                {
                    dataBuilder.Append('\n');
                }

                dataBuilder.Append(data);
            }

            if (dataBuilder.Length > 0)
            {
                yield return DeserializeItem(dataBuilder.ToString(), jsonTypeInfo);
            }
        }
        finally
        {
            response.Dispose();
        }
    }

    private static T DeserializeItem<T>(string payload, JsonTypeInfo<T> jsonTypeInfo)
    {
        if (typeof(T) == typeof(string))
        {
            return (T)(object)payload;
        }

        return JsonSerializer.Deserialize(payload, jsonTypeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize SSE payload to '{typeof(T).FullName}'.");
    }
}
