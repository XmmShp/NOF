using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Abstraction;
using NOF.Domain;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class NOFDomainExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddNOFDomain(IIdGenerator generator)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(generator);

            services.AddNOFAbstraction();
            services.TryAddSingleton(generator);
            services.TryAddEnumerable(new ServiceDescriptor(
                typeof(IDaemonService),
                typeof(IdGeneratorAmbientDaemonService),
                ServiceLifetime.Scoped));
            return services;
        }

        public IServiceCollection AddNOFDomain(
            uint applicationId,
            uint instanceId,
            Action<SnowflakeIdGeneratorOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddNOFAbstraction();

            var options = new SnowflakeIdGeneratorOptions();
            configure?.Invoke(options);
            ValidateSnowflakeIdGeneratorOptions(options);

            services.TryAddSingleton<IIdGenerator>(_ => new SnowflakeIdGenerator(options, applicationId, instanceId));
            services.TryAddEnumerable(new ServiceDescriptor(
                typeof(IDaemonService),
                typeof(IdGeneratorAmbientDaemonService),
                ServiceLifetime.Scoped));
            return services;
        }

        private static void ValidateSnowflakeIdGeneratorOptions(SnowflakeIdGeneratorOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (options.ApplicationIdBits <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "ApplicationIdBits must be greater than zero.");
            }

            if (options.InstanceIdBits <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "InstanceIdBits must be greater than zero.");
            }

            if (options.SequenceBits <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "SequenceBits must be greater than zero.");
            }

            if (options.ApplicationIdBits + options.InstanceIdBits + options.SequenceBits > 22)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "The sum of ApplicationIdBits, InstanceIdBits, and SequenceBits must be less than or equal to 22.");
            }
        }
    }
}
