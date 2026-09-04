using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using System.Runtime.CompilerServices;

namespace NOF.Infrastructure;

/// <summary>
/// Applies NOF object-key conventions over a concrete storage rider.
/// </summary>
public sealed class ObjectStorageService : IObjectStorage
{
    private readonly IObjectStorageRider _rider;
    private readonly ObjectStorageOptions _options;
    private readonly ICurrentTenant _currentTenant;
    private readonly bool _ignoreKeyPrefix;

    /// <summary>
    /// Initializes a new object storage service.
    /// </summary>
    public ObjectStorageService(
        IObjectStorageRider rider,
        IOptions<ObjectStorageOptions> options,
        ICurrentTenant currentTenant)
        : this(
            rider,
            options?.Value ?? throw new ArgumentNullException(nameof(options)),
            currentTenant,
            ignoreKeyPrefix: false)
    {
    }

    private ObjectStorageService(
        IObjectStorageRider rider,
        ObjectStorageOptions options,
        ICurrentTenant currentTenant,
        bool ignoreKeyPrefix)
    {
        ArgumentNullException.ThrowIfNull(rider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(currentTenant);

        _rider = rider;
        _options = options;
        _currentTenant = currentTenant;
        _ignoreKeyPrefix = ignoreKeyPrefix;
    }

    /// <inheritdoc />
    public IObjectStorage IgnoreKeyPrefix()
        => _ignoreKeyPrefix
            ? this
            : new ObjectStorageService(_rider, _options, _currentTenant, ignoreKeyPrefix: true);

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

        var keyPrefix = GetKeyPrefix();
        var result = await _rider.PutAsync(
            bucketName,
            ApplyKeyPrefix(objectKey, keyPrefix),
            content,
            options,
            cancellationToken);
        return ToLogicalInfo(result, keyPrefix);
    }

    /// <inheritdoc />
    public async ValueTask<Optional<ObjectStorageReadResult>> OpenReadAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);

        var keyPrefix = GetKeyPrefix();
        var result = await _rider.OpenReadAsync(
            bucketName,
            ApplyKeyPrefix(objectKey, keyPrefix),
            cancellationToken);
        if (!result.HasValue)
        {
            return Optional.None;
        }

        return Optional.Of(new ObjectStorageReadResult(
            result.Value.Content,
            ToLogicalInfo(result.Value.ObjectInfo, keyPrefix)));
    }

    /// <inheritdoc />
    public async ValueTask<Optional<ObjectStorageObjectInfo>> GetInfoAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);

        var keyPrefix = GetKeyPrefix();
        var result = await _rider.GetInfoAsync(
            bucketName,
            ApplyKeyPrefix(objectKey, keyPrefix),
            cancellationToken);
        return result.HasValue
            ? Optional.Of(ToLogicalInfo(result.Value, keyPrefix))
            : Optional.None;
    }

    /// <inheritdoc />
    public ValueTask<bool> ExistsAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);
        return _rider.ExistsAsync(
            bucketName,
            ApplyKeyPrefix(objectKey, GetKeyPrefix()),
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(bucketName, objectKey);
        return _rider.DeleteAsync(
            bucketName,
            ApplyKeyPrefix(objectKey, GetKeyPrefix()),
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<Optional<ObjectStorageObjectInfo>> CopyAsync(
        string sourceBucketName,
        string sourceObjectKey,
        string destinationBucketName,
        string destinationObjectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateLocation(sourceBucketName, sourceObjectKey);
        ValidateLocation(destinationBucketName, destinationObjectKey);

        var keyPrefix = GetKeyPrefix();
        var result = await _rider.CopyAsync(
            sourceBucketName,
            ApplyKeyPrefix(sourceObjectKey, keyPrefix),
            destinationBucketName,
            ApplyKeyPrefix(destinationObjectKey, keyPrefix),
            cancellationToken);
        return result.HasValue
            ? Optional.Of(ToLogicalInfo(result.Value, keyPrefix))
            : Optional.None;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ObjectStorageObjectInfo> ListAsync(
        string bucketName,
        string? prefix = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);

        var keyPrefix = GetKeyPrefix();
        var physicalPrefix = ApplyKeyPrefix(prefix ?? string.Empty, keyPrefix);
        await foreach (var item in _rider
            .ListAsync(bucketName, physicalPrefix, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return ToLogicalInfo(item, keyPrefix);
        }
    }

    private string GetKeyPrefix()
    {
        if (_ignoreKeyPrefix || string.IsNullOrEmpty(_options.KeyPrefix))
        {
            return string.Empty;
        }

        return DbConnectionStringTemplateResolver.ResolveTenantId(
            _options.KeyPrefix,
            _currentTenant.TenantId);
    }

    private static string ApplyKeyPrefix(string objectKey, string keyPrefix)
        => keyPrefix + objectKey;

    private static ObjectStorageObjectInfo ToLogicalInfo(
        ObjectStorageObjectInfo info,
        string keyPrefix)
    {
        if (keyPrefix.Length == 0)
        {
            return info;
        }

        if (!info.ObjectKey.StartsWith(keyPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The object storage rider returned key '{info.ObjectKey}' outside the configured prefix.");
        }

        return info with { ObjectKey = info.ObjectKey[keyPrefix.Length..] };
    }

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
}
