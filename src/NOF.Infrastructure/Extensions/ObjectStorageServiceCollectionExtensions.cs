using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Infrastructure;

namespace NOF.Hosting;

public static partial class NOFInfrastructureExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Uses durable local object storage with an optional custom directory.</summary>
        public IServiceCollection AddFileSystemObjectStorage(Action<FileSystemObjectStorageOptions>? configureOptions = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            services.AddOptions<FileSystemObjectStorageOptions>();
            if (configureOptions is not null) services.Configure(configureOptions);
            services.ReplaceOrAddScoped<IObjectStorageRider, FileSystemObjectStorageRider>();
            return services;
        }

        /// <summary>Uses host-isolated, in-memory object storage, primarily for tests.</summary>
        public IServiceCollection AddMemoryObjectStorage()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<MemoryObjectStorageRiderState>();
            services.ReplaceOrAddScoped<IObjectStorageRider>(sp => new MemoryObjectStorageRider(
                sp.GetRequiredService<MemoryObjectStorageRiderState>()));
            return services;
        }
    }
}
