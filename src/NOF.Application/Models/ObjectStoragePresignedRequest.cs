using System.Collections.ObjectModel;

namespace NOF.Application;

/// <summary>Describes a temporary HTTP request for direct object upload or download.</summary>
public sealed class ObjectStoragePresignedRequest
{
    /// <summary>Initializes a presigned request, taking a snapshot of the required headers.</summary>
    public ObjectStoragePresignedRequest(string url, string method, DateTimeOffset expiresAt,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        Url = url;
        Method = method;
        ExpiresAt = expiresAt;
        Headers = new ReadOnlyDictionary<string, string>(
            headers is null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Gets the signed URL. Treat this value as a temporary access credential.</summary>
    public string Url { get; }

    /// <summary>Gets the HTTP method the client must use.</summary>
    public string Method { get; }

    /// <summary>
    /// Gets the requested UTC expiration. Provider credentials or policies may expire access sooner.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Gets the headers the client must send unchanged with the request.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }
}
