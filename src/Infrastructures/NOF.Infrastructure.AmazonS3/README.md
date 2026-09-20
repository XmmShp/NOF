# NOF.Infrastructure.AmazonS3

AWS S3 and S3-compatible object storage provider for the [NOF Framework](https://github.com/XmmShp/NOF).

## AWS S3

```csharp
builder.Services.AddAmazonS3ObjectStorage(options =>
{
    options.Region = "ap-southeast-1";
});
```

When credentials are not configured explicitly, the AWS SDK default credential chain is used.

## S3-compatible services

MinIO, Ceph, Cloudflare R2, and similar endpoints can be configured with a service URL and path-style addressing:

```csharp
builder.Services.AddAmazonS3ObjectStorage(options =>
{
    options.ServiceUrl = "https://s3.example.com";
    options.Region = "us-east-1";
    options.ForcePathStyle = true;
    options.AccessKeyId = configuration["S3:AccessKeyId"];
    options.SecretAccessKey = configuration["S3:SecretAccessKey"];
    options.UseChunkEncoding = false;
});
```

When `ServiceUrl` is configured without `Region`, request signing defaults to `us-east-1`.

Application handlers continue to inject `IObjectStorage`; the provider replaces only `IObjectStorageRider`.

For advanced client configuration, supply an existing client or a service-provider factory:

```csharp
builder.Services.AddAmazonS3ObjectStorage(serviceProvider =>
    new AmazonS3Client(customCredentials, customConfig));
```

## Direct browser uploads and downloads

`IObjectStorage.SupportsPresignedRequests` is `true` for this provider. Authorize the user and
choose the bucket and logical object key on the server, then issue a short-lived request:

```csharp
var upload = await objectStorage.CreatePresignedUploadAsync(
    "documents", $"uploads/{Guid.NewGuid():N}.pdf", TimeSpan.FromMinutes(10),
    new ObjectStorageWriteOptions { ContentType = "application/pdf" }, cancellationToken);

var download = await objectStorage.CreatePresignedDownloadAsync(
    "documents", "reports/42.pdf", TimeSpan.FromMinutes(5), cancellationToken);
```

Both operations apply `ObjectStorageOptions.KeyPrefix`, including `{tenantId}`, just like
`PutAsync` and `OpenReadAsync`. `IgnoreKeyPrefix()` remains an explicit administrative bypass.
Return the presigned request's `Url`, `Method`, `Headers`, and `ExpiresAt` to the client.
The URL is opaque and contains temporary authorization; do not rewrite its host or path, or log it.
Configure the S3 client with an endpoint reachable by browsers before signing.

With the default camel-case JSON naming policy, upload the raw file body in the browser:

```javascript
const response = await fetch(upload.url, {
  method: upload.method,
  headers: upload.headers,
  body: file
});
if (!response.ok) throw new Error(`Upload failed: ${response.status}`);
```

Send the returned headers unchanged. Upload content headers and custom metadata are included in
the signature. Configure bucket CORS to allow the frontend origin, PUT/GET methods, and required
headers; a valid signature does not bypass browser CORS. A presigned PUT can overwrite the same
key and can be reused until it expires. Signing does not upload an object or check download
existence. This API provides single-request PUT/GET signing, not multipart upload or POST policies;
it does not enforce an upload size limit or perform post-upload validation.

Lifetimes must be between one second and seven days; expiration is rounded down to a UTC second.
Temporary credential expiry or storage policies may make a request expire earlier than `ExpiresAt`.
See the [AWS presigned URL guide](https://docs.aws.amazon.com/AmazonS3/latest/userguide/using-presigned-url.html).
The SDK signing API does not accept a cancellation token; NOF observes cancellation while waiting
for signing, but underlying credential resolution may continue.

## Installation

```shell
dotnet add package NOF.Infrastructure.AmazonS3
```

## License

Apache-2.0
