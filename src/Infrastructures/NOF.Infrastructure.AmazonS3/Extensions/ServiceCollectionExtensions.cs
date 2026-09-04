using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NOF.Infrastructure;
using NOF.Infrastructure.AmazonS3;

namespace NOF.Hosting;

/// <summary>
/// Registers AWS S3 object storage infrastructure.
/// </summary>
public static partial class NOFInfrastructureExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Replaces the default object storage rider with an AWS S3 or S3-compatible provider.
        /// </summary>
        public IServiceCollection AddAmazonS3ObjectStorage(
            Action<AmazonS3ObjectStorageOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            ConfigureProvider(services, configureOptions);
            services.ReplaceOrAddSingleton<IAmazonS3>(static serviceProvider =>
                AmazonS3ClientFactory.Create(
                    serviceProvider
                        .GetRequiredService<IOptions<AmazonS3ObjectStorageOptions>>()
                        .Value));
            RegisterRider(services);
            return services;
        }

        /// <summary>
        /// Replaces the default object storage rider and uses the supplied S3 client factory.
        /// </summary>
        public IServiceCollection AddAmazonS3ObjectStorage(
            Func<IServiceProvider, IAmazonS3> clientFactory,
            Action<AmazonS3ObjectStorageOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(clientFactory);

            ConfigureProvider(services, configureOptions);
            services.ReplaceOrAddSingleton(clientFactory);
            RegisterRider(services);
            return services;
        }

        /// <summary>
        /// Replaces the default object storage rider and uses an existing S3 client.
        /// </summary>
        public IServiceCollection AddAmazonS3ObjectStorage(
            IAmazonS3 client,
            Action<AmazonS3ObjectStorageOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(client);

            ConfigureProvider(services, configureOptions);
            services.ReplaceOrAddSingleton(client);
            RegisterRider(services);
            return services;
        }
    }

    private static void ConfigureProvider(
        IServiceCollection services,
        Action<AmazonS3ObjectStorageOptions>? configureOptions)
    {
        services.AddOptions<AmazonS3ObjectStorageOptions>();
        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }
    }

    private static void RegisterRider(IServiceCollection services)
        => services.ReplaceOrAddScoped<IObjectStorageRider, AmazonS3ObjectStorageRider>();
}
