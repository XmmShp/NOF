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
        foreach (var (headerName, value) in normalizedHeaders)
        {
            metadatas[CreateHeaderMetadataKey(headerName)] = value;
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

        var headers = metadatas
            .Where(static pair => pair.Key.StartsWith(
                NOFAbstractionConstants.Transport.Metadatas.HttpHeaderPrefix,
                StringComparison.OrdinalIgnoreCase))
            .Select(static pair => new KeyValuePair<string, string?>(
                pair.Key[NOFAbstractionConstants.Transport.Metadatas.HttpHeaderPrefix.Length..],
                pair.Value));

        var normalizedHeaders = NormalizeHeaders(headers);
        if (normalizedHeaders.Count == 0)
        {
            return EmptyHeaders;
        }

        return normalizedHeaders;
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

    private static string CreateHeaderMetadataKey(string headerName)
        => NOFAbstractionConstants.Transport.Metadatas.HttpHeaderPrefix + headerName;
}
