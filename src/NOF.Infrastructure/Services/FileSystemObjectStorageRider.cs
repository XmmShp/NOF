using Microsoft.Extensions.Options;
using NOF.Application;
using NOF.Contract;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NOF.Infrastructure;

/// <summary>
/// Stores objects and metadata in atomic, provider-owned files on the local file system.
/// Bucket names and keys are case-sensitive and are hashed rather than interpreted as paths.
/// </summary>
public sealed class FileSystemObjectStorageRider : IObjectStorageRider
{
    private readonly string _rootPath;

    /// <summary>Initializes a rider using the configured storage directory.</summary>
    public FileSystemObjectStorageRider(IOptions<FileSystemObjectStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.RootPath);
        _rootPath = Path.GetFullPath(options.Value.RootPath);
    }

    /// <inheritdoc />
    public async ValueTask<ObjectStorageObjectInfo> PutAsync(string bucketName, string objectKey,
        Stream content, ObjectStorageWriteOptions? options = null, CancellationToken cancellationToken = default)
    {
        var path = GetPath(bucketName, objectKey);
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
        {
            throw new ArgumentException("The object content stream must be readable.", nameof(content));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        var contentPath = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await using var buffer = new FileStream(contentPath, FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            await content.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            var hash = await SHA256.HashDataAsync(buffer, cancellationToken);
            var info = new ObjectStorageObjectInfo(bucketName, objectKey, buffer.Length, DateTimeOffset.UtcNow,
                Convert.ToHexString(hash).ToLowerInvariant(), options?.ContentType, options?.ContentEncoding,
                options?.CacheControl, options?.ContentDisposition, options?.Metadata);
            var metadata = JsonSerializer.SerializeToUtf8Bytes(info, FileSystemObjectStorageJsonContext.Default.ObjectStorageObjectInfo);
            var header = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(header, metadata.Length);
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await output.WriteAsync(header, cancellationToken);
                await output.WriteAsync(metadata, cancellationToken);
                buffer.Position = 0;
                await buffer.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            Commit(temporaryPath, path, cancellationToken);
            return info;
        }
        finally
        {
            File.Delete(temporaryPath);
            File.Delete(contentPath);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Optional<ObjectStorageReadResult>> OpenReadAsync(string bucketName, string objectKey,
        CancellationToken cancellationToken = default)
    {
        var path = GetPath(bucketName, objectKey);
        cancellationToken.ThrowIfCancellationRequested();
        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (FileNotFoundException) { return Optional.None; }
        catch (DirectoryNotFoundException) { return Optional.None; }
        try
        {
            var header = new byte[sizeof(int)];
            await stream.ReadExactlyAsync(header, cancellationToken);
            var length = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (length <= 0 || length > stream.Length - sizeof(int))
            {
                throw new InvalidDataException("Invalid object metadata length.");
            }
            var metadata = new byte[length];
            await stream.ReadExactlyAsync(metadata, cancellationToken);
            var info = JsonSerializer.Deserialize(metadata, FileSystemObjectStorageJsonContext.Default.ObjectStorageObjectInfo)
                ?? throw new InvalidDataException("Missing object metadata.");
            if (info.BucketName != bucketName || info.ObjectKey != objectKey || info.ContentLength != stream.Length - stream.Position)
            {
                throw new InvalidDataException("Object metadata does not match the stored content.");
            }
            // Hide the metadata header, including from callers that seek or inspect Length.
            return Optional.Of(new ObjectStorageReadResult(new ContentStream(stream), info));
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<Optional<ObjectStorageObjectInfo>> GetInfoAsync(string bucketName, string objectKey,
        CancellationToken cancellationToken = default)
    {
        var result = await OpenReadAsync(bucketName, objectKey, cancellationToken);
        if (!result.HasValue)
        {
            return Optional.None;
        }

        await using var content = result.Value.Content;
        return Optional.Of(result.Value.ObjectInfo);
    }

    /// <inheritdoc />
    public async ValueTask<bool> ExistsAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default)
        => (await GetInfoAsync(bucketName, objectKey, cancellationToken)).HasValue;

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default)
    {
        var path = GetPath(bucketName, objectKey);
        cancellationToken.ThrowIfCancellationRequested();
        var tombstone = Path.Combine(Path.GetDirectoryName(path)!, $"{Guid.NewGuid():N}.tmp");
        try
        { File.Move(path, tombstone); }
        catch (FileNotFoundException) { return ValueTask.FromResult(false); }
        catch (DirectoryNotFoundException) { return ValueTask.FromResult(false); }
        File.Delete(tombstone);
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc />
    public async ValueTask<Optional<ObjectStorageObjectInfo>> CopyAsync(string sourceBucketName, string sourceObjectKey,
        string destinationBucketName, string destinationObjectKey, CancellationToken cancellationToken = default)
    {
        _ = GetPath(destinationBucketName, destinationObjectKey);
        var source = await OpenReadAsync(sourceBucketName, sourceObjectKey, cancellationToken);
        if (!source.HasValue)
        {
            return Optional.None;
        }

        await using var content = source.Value.Content;
        var info = source.Value.ObjectInfo;
        return Optional.Of(await PutAsync(destinationBucketName, destinationObjectKey, content,
            new ObjectStorageWriteOptions
            {
                ContentType = info.ContentType,
                ContentEncoding = info.ContentEncoding,
                CacheControl = info.CacheControl,
                ContentDisposition = info.ContentDisposition,
                Metadata = info.Metadata
            }, cancellationToken));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ObjectStorageObjectInfo> ListAsync(string bucketName, string? prefix = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.Combine(_rootPath, Hash(bucketName));
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        var items = new List<ObjectStorageObjectInfo>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.obj"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Read the header to recover the original key from the hashed file name.
            byte[] metadata;
            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
                var header = new byte[sizeof(int)];
                await stream.ReadExactlyAsync(header, cancellationToken);
                var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (length <= 0 || length > stream.Length - sizeof(int))
                {
                    throw new InvalidDataException("Invalid object metadata length.");
                }

                metadata = new byte[length];
                await stream.ReadExactlyAsync(metadata, cancellationToken);
            }
            catch (FileNotFoundException) { continue; }
            var info = JsonSerializer.Deserialize(metadata, FileSystemObjectStorageJsonContext.Default.ObjectStorageObjectInfo)
                ?? throw new InvalidDataException("Missing object metadata.");
            if (info.BucketName == bucketName && info.ObjectKey.StartsWith(prefix ?? string.Empty, StringComparison.Ordinal))
            {
                items.Add(info);
            }
        }
        foreach (var info in items.OrderBy(static item => item.ObjectKey, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return info;
        }
    }

    private static void Commit(string temporaryPath, string path, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Move(temporaryPath, path);
                return;
            }
            catch (IOException) when (File.Exists(path))
            {
                try
                {
                    // ReplaceFile preserves open reader handles on Windows as well as Unix.
                    File.Replace(temporaryPath, path, destinationBackupFileName: null);
                    return;
                }
                catch (FileNotFoundException) when (File.Exists(temporaryPath))
                {
                    // A concurrent delete removed the destination; retry creation.
                }
            }
        }
    }

    private string GetPath(string bucketName, string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        return Path.Combine(_rootPath, Hash(bucketName), Hash(objectKey) + ".obj");
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class ContentStream(FileStream inner) : Stream
    {
        private readonly long _offset = inner.Position;
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length - _offset;
        public override long Position { get => inner.Position - _offset; set => Seek(value, SeekOrigin.Begin); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin)
        {
            var position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(Position + offset),
                SeekOrigin.End => checked(Length + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            if (position < 0)
            {
                throw new IOException("Cannot seek before the object content.");
            }

            return inner.Seek(checked(_offset + position), SeekOrigin.Begin) - _offset;
        }
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) { inner.Dispose(); } base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() { await inner.DisposeAsync(); GC.SuppressFinalize(this); }
    }
}
/// <summary>Provides generated JSON metadata for local object storage records.</summary>

[JsonSerializable(typeof(ObjectStorageObjectInfo))]
public partial class FileSystemObjectStorageJsonContext : JsonSerializerContext;
