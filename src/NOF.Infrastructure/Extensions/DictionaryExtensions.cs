using MassTransit;
using System.Reflection;

namespace NOF;

public static class DictionaryExtensions
{
    extension(IDictionary<string, object?> metadata)
    {
        public T GetOrAdd<T>(string name, Func<T> valueFactory)
        {
            if (metadata.TryGetValue(name, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            var defaultValue = valueFactory();
            metadata[name] = defaultValue;
            return defaultValue;
        }

        public T Get<T>(string name)
        {
            return (T)metadata[name]!;
        }

        public List<Assembly> Assemblies => metadata.GetOrAdd("Assemblies", () => new List<Assembly>());
        internal List<Action<IBusRegistrationConfigurator>> MassTransitConfigurations
            => metadata.GetOrAdd("MassTransitConfigurations", () => new List<Action<IBusRegistrationConfigurator>>());
    }
}
