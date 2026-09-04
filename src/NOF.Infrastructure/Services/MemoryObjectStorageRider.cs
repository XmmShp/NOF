using NOF.Application;
using NOF.Contract;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace NOF.Infrastructure;

/// <summary>
/// Provides host-local in-memory object storage for development and tests.
/// </summary>
public sealed class MemoryObjectStorageRider : IObjectStorageRider, IDisposable
{
    private readonly MemoryObjectStorageRiderState _state;
    private readonly bool _ownsState;

    /// <summary>
    /// Initializes an isolated in-memory object storage rider.
    /// </summary>
    public MemoryObjectStorageRider()
        : this(new MemoryObjectStorageRiderState())
    {
        _ownsState = true;
    }

    /// <summary>
    /// Initializes an in-memory object storage rider over shared host state.
    /// </summary>
    public MemoryObjectStorageRider(MemoryObjectStorageRiderState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <inheritdoc />
    public async ValueTask<ObjectStorageObjectInfo> PutAsync(
        string bucketName,
        string objectKey,
        Stream content,
        ObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);
        ValidateContent(content);
        cancellationToken.ThrowIfCancellationRequested();

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var info = CreateInfo(bucketName, objectKey, bytes, options);
        _state.Objects[new ObjectIdentifier(bucketName, objectKey)] = new StoredObject(bytes, info);
        return info;
    }

    /// <inheritdoc />
    public ValueTask<Optional<ObjectStorageReadResult>> OpenReadAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_state.Objects.TryGetValue(new ObjectIdentifier(bucketName, objectKey), out var storedObject))
        {
            return ValueTask.FromResult<Optional<ObjectStorageReadResult>>(Optional.None);
        }

        Stream content = new MemoryStream(storedObject.Content, writable: false);
        return ValueTask.FromResult(Optional.Of(
            new ObjectStorageReadResult(content, storedObject.Info)));
    }

    /// <inheritdoc />
    public ValueTask<Optional<ObjectStorageObjectInfo>> GetInfoAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(
            _state.Objects.TryGetValue(new ObjectIdentifier(bucketName, objectKey), out var storedObject)
                ? Optional.Of(storedObject.Info)
                : Optional.None);
    }

    /// <inheritdoc />
    public ValueTask<bool> ExistsAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            _state.Objects.ContainsKey(new ObjectIdentifier(bucketName, objectKey)));
    }

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            _state.Objects.TryRemove(new ObjectIdentifier(bucketName, objectKey), out _));
    }

    /// <inheritdoc />
    public ValueTask<Optional<ObjectStorageObjectInfo>> CopyAsync(
        string sourceBucketName,
        string sourceObjectKey,
        string destinationBucketName,
        string destinationObjectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(sourceBucketName, sourceObjectKey);
        ValidateLocation(destinationBucketName, destinationObjectKey);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_state.Objects.TryGetValue(
            new ObjectIdentifier(sourceBucketName, sourceObjectKey),
            out var source))
        {
            return ValueTask.FromResult<Optional<ObjectStorageObjectInfo>>(Optional.None);
        }

        var bytes = source.Content.ToArray();
        var destinationInfo = source.Info with
        {
            BucketName = destinationBucketName,
            ObjectKey = destinationObjectKey,
            LastModified = DateTimeOffset.UtcNow,
            Metadata = CopyMetadata(source.Info.Metadata)
        };
        _state.Objects[new ObjectIdentifier(destinationBucketName, destinationObjectKey)] =
            new StoredObject(bytes, destinationInfo);
        return ValueTask.FromResult(Optional.Of(destinationInfo));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ObjectStorageObjectInfo> ListAsync(
        string bucketName,
        string? prefix = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPrefix = prefix ?? string.Empty;
        var items = _state.Objects
            .Where(pair => string.Equals(pair.Key.BucketName, bucketName, StringComparison.Ordinal)
                && pair.Key.ObjectKey.StartsWith(normalizedPrefix, StringComparison.Ordinal))
            .OrderBy(static pair => pair.Key.ObjectKey, StringComparer.Ordinal)
            .Select(static pair => pair.Value.Info)
            .ToArray();

        await Task.CompletedTask;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsState)
        {
            _state.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private static ObjectStorageObjectInfo CreateInfo(
        string bucketName,
        string objectKey,
        byte[] content,
        ObjectStorageWriteOptions? options)
    {
        var entityTag = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new ObjectStorageObjectInfo(
            bucketName,
            objectKey,
            content.LongLength,
            DateTimeOffset.UtcNow,
            entityTag,
            options?.ContentType,
            options?.ContentEncoding,
            options?.CacheControl,
            options?.ContentDisposition,
            options?.Metadata);
    }

    private static IReadOnlyDictionary<string, string> CopyMetadata(
        IReadOnlyDictionary<string, string> metadata)
        => metadata.Count == 0
            ? ReadOnlyDictionary<string, string>.Empty
            : new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase));

    private static void ValidateLocation(string bucketName, string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
    }

    private static void ValidateContent(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
        {
            throw new ArgumentException("The object content stream must be readable.", nameof(content));
        }
    }

    internal readonly record struct ObjectIdentifier(string BucketName, string ObjectKey);

    internal sealed record StoredObject(byte[] Content, ObjectStorageObjectInfo Info);
}
