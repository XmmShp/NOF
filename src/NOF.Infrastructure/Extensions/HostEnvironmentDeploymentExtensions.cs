using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Runtime.CompilerServices;

namespace NOF.Infrastructure;

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

    extension(IHostEnvironment environment)
    {
        public uint ApplicationId
        {
            get => HostEnvironmentExtensionBag.GetOrAdd(environment, ApplicationIdKey, static () => 0u);
            set => HostEnvironmentExtensionBag.Set(environment, ApplicationIdKey, value);
        }

        public uint InstanceId
        {
            get => HostEnvironmentExtensionBag.GetOrAdd(environment, InstanceIdKey, static () => 0u);
            set => HostEnvironmentExtensionBag.Set(environment, InstanceIdKey, value);
        }

        public void BindConfiguration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            environment.ApplicationName = configuration.GetValue<string>(
                NOFInfrastructureConstants.Deployment.ConfigurationKeys.ApplicationName) ?? environment.ApplicationName;
            environment.ApplicationId = configuration.GetValue<uint?>(
                NOFInfrastructureConstants.Deployment.ConfigurationKeys.ApplicationId) ?? environment.ApplicationId;
            environment.InstanceId = configuration.GetValue<uint?>(
                NOFInfrastructureConstants.Deployment.ConfigurationKeys.InstanceId) ?? environment.InstanceId;
        }
    }
}
