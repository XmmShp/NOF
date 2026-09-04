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

## Installation

```shell
dotnet add package NOF.Infrastructure.AmazonS3
```

## License

Apache-2.0
