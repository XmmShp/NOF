using NOF.Abstraction;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class NOFAbstractionExtensions
{
    extension(IServiceCollection services)
    {
        public T GetOrAddSingleton<T>() where T : class, new()
        {
            ArgumentNullException.ThrowIfNull(services);

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(T));
            if (descriptor?.ImplementationInstance is T existing)
            {
                return existing;
            }

            var instance = new T();
            services.AddSingleton(instance);
            return instance;
        }

        public T GetOrAddSingleton<T>(Func<T> factory) where T : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(factory);

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(T));
            if (descriptor?.ImplementationInstance is T existing)
            {
                return existing;
            }

            var instance = factory();
            services.AddSingleton(instance);
            return instance;
        }

        public InitializedTypes InitializedTypes
            => services.GetOrAddSingleton<InitializedTypes>();
    }
}
