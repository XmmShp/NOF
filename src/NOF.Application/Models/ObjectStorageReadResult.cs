namespace NOF.Application;

/// <summary>
/// Contains a readable object stream and its metadata.
/// </summary>
public sealed class ObjectStorageReadResult
{
    /// <summary>
    /// Initializes a readable object result.
    /// </summary>
    public ObjectStorageReadResult(Stream content, ObjectStorageObjectInfo objectInfo)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(objectInfo);

        if (!content.CanRead)
        {
            throw new ArgumentException("The object content stream must be readable.", nameof(content));
        }

        Content = content;
        ObjectInfo = objectInfo;
    }

    /// <summary>
    /// Gets the readable content stream. The caller owns and must dispose this stream.
    /// </summary>
    public Stream Content { get; }

    /// <summary>Gets metadata for the returned object.</summary>
    public ObjectStorageObjectInfo ObjectInfo { get; }
}
