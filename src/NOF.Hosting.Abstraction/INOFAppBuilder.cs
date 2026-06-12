using NOF.Abstraction;
using System.Reflection;

namespace NOF.Hosting;

public interface INOFAppBuilder : IServiceRegistrationContext
{
    INOFAppBuilder AddRegistrationStep(IServiceRegistrationStep registrationStep);

    INOFAppBuilder RemoveRegistrationStep(Predicate<IServiceRegistrationStep> predicate);
}

public static partial class NOFHostingExtensions
{
    private static readonly object ApplicationPartsKey = new();

    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddApplicationPart(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            var loadedAssemblies = GetOrAddApplicationParts(builder);
            if (!loadedAssemblies.Add(assembly))
            {
                return builder;
            }

            foreach (var attribute in assembly.GetCustomAttributes<AssemblyInitializeAttribute>())
            {
                attribute.Initialize(builder.Services);
            }

            return builder;
        }

        public INOFAppBuilder RemoveRegistrationStep<T>() where T : IServiceRegistrationStep
            => builder.RemoveRegistrationStep(t => t is T);

        public INOFAppBuilder TryAddRegistrationStep(IServiceRegistrationStep registrationStep)
        {
            ArgumentNullException.ThrowIfNull(registrationStep);
            var exists = false;
            builder.RemoveRegistrationStep(existing =>
            {
                if (existing.GetType() == registrationStep.GetType())
                {
                    exists = true;
                }
                return false;
            });

            if (exists)
            {
                return builder;
            }

            return builder.AddRegistrationStep(registrationStep);
        }

        public INOFAppBuilder TryAddRegistrationStep<T>() where T : IServiceRegistrationStep, new()
            => builder.TryAddRegistrationStep(new T());

    }

    private static HashSet<Assembly> GetOrAddApplicationParts(INOFAppBuilder builder)
    {
        if (builder.Properties.TryGetValue(ApplicationPartsKey, out var existing) && existing is HashSet<Assembly> assemblies)
        {
            return assemblies;
        }

        assemblies = [];
        builder.Properties[ApplicationPartsKey] = assemblies;
        return assemblies;
    }
}
