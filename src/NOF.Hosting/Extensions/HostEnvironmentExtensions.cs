using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.Hosting;

internal static class HostEnvironmentExtensionBag
{
    private static readonly ConditionalWeakTable<object, Dictionary<string, object>> _packages = [];

    internal static T GetOrAdd<T>(object owner, string key, Func<T> valueFactory)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(valueFactory);

        var package = _packages.GetOrCreateValue(owner);
        lock (package)
        {
            if (package.TryGetValue(key, out var existingValue))
            {
                return (T)existingValue;
            }

            var createdValue = valueFactory();
            package[key] = createdValue;
            return createdValue;
        }
    }

    internal static void Set(object owner, string key, object value)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var package = _packages.GetOrCreateValue(owner);
        lock (package)
        {
            package[key] = value;
        }
    }
}

public static class HostEnvironmentExtensions
{
    private const string ApplicationIdKey = "ApplicationId";
    private const string InstanceIdKey = "InstanceId";
    private const string IsPrimaryNodeEnvironmentPredicatorKey = "IsPrimaryNodeEnvironmentPredicator";

    extension(IHostEnvironment environment)
    {
        public uint ApplicationId
        {
            get => HostEnvironmentExtensionBag.GetOrAdd(environment, ApplicationIdKey, static () => 0u);
            set => HostEnvironmentExtensionBag.Set(environment, ApplicationIdKey, value);
        }

        public uint InstanceId
        {
            get => HostEnvironmentExtensionBag.GetOrAdd(environment, InstanceIdKey, static () => 1u);
            set => HostEnvironmentExtensionBag.Set(environment, InstanceIdKey, value);
        }

        public bool IsPrimaryNodeEnvironment
        {
            get => HostEnvironmentExtensionBag.GetOrAdd<Predicate<IHostEnvironment>>(
                environment,
                IsPrimaryNodeEnvironmentPredicatorKey,
                static () => env => env.InstanceId == 1)(environment);
        }

        public void SetPrimaryNodeEnvironmentPredicator(Predicate<IHostEnvironment> predicator)
        {
            ArgumentNullException.ThrowIfNull(predicator);
            HostEnvironmentExtensionBag.Set(environment, IsPrimaryNodeEnvironmentPredicatorKey, predicator);
        }

        public void BindConfiguration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            environment.ApplicationName = configuration[NOF.Hosting.NOFHostingConstants.Deployment.ConfigurationKeys.ApplicationName]
                ?? environment.ApplicationName;
            if (uint.TryParse(configuration[NOF.Hosting.NOFHostingConstants.Deployment.ConfigurationKeys.ApplicationId], out var applicationId))
            {
                environment.ApplicationId = applicationId;
            }

            if (uint.TryParse(configuration[NOF.Hosting.NOFHostingConstants.Deployment.ConfigurationKeys.InstanceId], out var instanceId))
            {
                environment.InstanceId = instanceId;
            }
        }
    }
}
