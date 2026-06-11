using System.Globalization;

namespace NOF.Abstraction;

public static class HttpTransportMetadata
{
    public static IReadOnlyDictionary<string, string?> EmptyHeaders { get; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string?> Create(
        int? statusCode = null,
        IEnumerable<KeyValuePair<string, string?>>? headers = null)
    {
        var metadatas = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (statusCode.HasValue)
        {
            metadatas[NOFAbstractionConstants.Transport.Metadatas.HttpStatusCode] =
                statusCode.Value.ToString(CultureInfo.InvariantCulture);
        }

        var normalizedHeaders = NormalizeHeaders(headers);
        if (normalizedHeaders.Count > 0)
        {
            metadatas[NOFAbstractionConstants.Transport.Metadatas.HttpHeaders] = SerializeHeaders(normalizedHeaders);
        }

        return metadatas;
    }

    public static bool TryGetStatusCode(IReadOnlyDictionary<string, string?> metadatas, out int statusCode)
    {
        ArgumentNullException.ThrowIfNull(metadatas);
        statusCode = default;

        return metadatas.TryGetValue(NOFAbstractionConstants.Transport.Metadatas.HttpStatusCode, out var rawValue)
            && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out statusCode);
    }

    public static IReadOnlyDictionary<string, string?> GetHeaders(IReadOnlyDictionary<string, string?> metadatas)
    {
        ArgumentNullException.ThrowIfNull(metadatas);

        if (!metadatas.TryGetValue(NOFAbstractionConstants.Transport.Metadatas.HttpHeaders, out var rawValue)
            || string.IsNullOrWhiteSpace(rawValue))
        {
            return EmptyHeaders;
        }

        return NormalizeHeaders(DeserializeHeaders(rawValue));
    }

    private static IReadOnlyDictionary<string, string?> NormalizeHeaders(IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        if (headers is null)
        {
            return EmptyHeaders;
        }

        var copied = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in headers)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            copied[key] = value;
        }

        return copied.Count == 0
            ? EmptyHeaders
            : copied;
    }

    private static string SerializeHeaders(IReadOnlyDictionary<string, string?> headers)
        => string.Join(
            '\n',
            headers.Select(static pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value ?? string.Empty)}"));

    private static IEnumerable<KeyValuePair<string, string?>> DeserializeHeaders(string rawValue)
    {
        foreach (var entry in rawValue.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = entry.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(entry[..separatorIndex]);
            var value = Uri.UnescapeDataString(entry[(separatorIndex + 1)..]);
            yield return new KeyValuePair<string, string?>(key, value);
        }
    }
}
