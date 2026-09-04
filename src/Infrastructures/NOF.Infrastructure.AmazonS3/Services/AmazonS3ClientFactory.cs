using Amazon;
using Amazon.Runtime;
using Amazon.S3;

namespace NOF.Infrastructure.AmazonS3;

internal static class AmazonS3ClientFactory
{
    public static IAmazonS3 Create(AmazonS3ObjectStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);

        var config = new AmazonS3Config
        {
            ForcePathStyle = options.ForcePathStyle
        };

        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.ServiceURL = options.ServiceUrl;
            config.AuthenticationRegion = string.IsNullOrWhiteSpace(options.Region)
                ? RegionEndpoint.USEast1.SystemName
                : options.Region;
        }
        else if (!string.IsNullOrWhiteSpace(options.Region))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }

        var credentials = CreateCredentials(options);
        return credentials is null
            ? new AmazonS3Client(config)
            : new AmazonS3Client(credentials, config);
    }

    private static AWSCredentials? CreateCredentials(AmazonS3ObjectStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AccessKeyId))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(options.SessionToken)
            ? new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey)
            : new SessionAWSCredentials(
                options.AccessKeyId,
                options.SecretAccessKey,
                options.SessionToken);
    }

    private static void Validate(AmazonS3ObjectStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Region)
            && string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            throw new InvalidOperationException(
                $"Either {nameof(options.Region)} or {nameof(options.ServiceUrl)} must be configured for Amazon S3 object storage.");
        }

        if (!string.IsNullOrWhiteSpace(options.ServiceUrl)
            && (!Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out var serviceUri)
                || (!string.Equals(serviceUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(serviceUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException(
                $"{nameof(options.ServiceUrl)} must be an absolute HTTP or HTTPS URL.");
        }

        var hasAccessKey = !string.IsNullOrWhiteSpace(options.AccessKeyId);
        var hasSecretKey = !string.IsNullOrWhiteSpace(options.SecretAccessKey);
        if (hasAccessKey != hasSecretKey)
        {
            throw new InvalidOperationException(
                $"{nameof(options.AccessKeyId)} and {nameof(options.SecretAccessKey)} must be configured together.");
        }

        if (!string.IsNullOrWhiteSpace(options.SessionToken) && !hasAccessKey)
        {
            throw new InvalidOperationException(
                $"{nameof(options.SessionToken)} requires explicit access-key credentials.");
        }
    }
}
