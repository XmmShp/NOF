using NOF.Abstraction;
using NOF.Annotation;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Hosting;

public interface INOFAppBuilder : IServiceRegistrationContext
{
    INOFAppBuilder AddRegistrationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep registrationStep, params Type[] allInterfaces)
        where TStep : IServiceRegistrationStep;

    INOFAppBuilder RemoveRegistrationStep(Predicate<IServiceRegistrationStep> predicate);

    new INOFAppBuilder AddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep initializationStep, params Type[] allInterfaces)
        where TStep : IApplicationInitializationStep;

    new INOFAppBuilder RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate);
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
                attribute.Initialize(builder.Registry);
            }

            return builder;
        }

        public INOFAppBuilder AddRegistrationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep registrationStep)
            where TStep : IServiceRegistrationStep
            => builder.AddRegistrationStep(registrationStep, [.. typeof(TStep).GetAllAssignableTypes()]);

        public INOFAppBuilder RemoveRegistrationStep<T>() where T : IServiceRegistrationStep
            => builder.RemoveRegistrationStep(t => t is T);

        public INOFAppBuilder TryAddRegistrationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep registrationStep, params Type[] allInterfaces)
            where TStep : IServiceRegistrationStep
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

            return builder.AddRegistrationStep(registrationStep, allInterfaces);
        }

        public INOFAppBuilder TryAddRegistrationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep registrationStep)
            where TStep : IServiceRegistrationStep
            => builder.TryAddRegistrationStep(registrationStep, [.. typeof(TStep).GetAllAssignableTypes()]);

        public INOFAppBuilder TryAddRegistrationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] T>() where T : IServiceRegistrationStep, new()
            => builder.TryAddRegistrationStep(new T());

        public INOFAppBuilder AddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep initializationStep)
            where TStep : IApplicationInitializationStep
            => builder.AddInitializationStep(initializationStep, [.. typeof(TStep).GetAllAssignableTypes()]);

        public INOFAppBuilder RemoveInitializationStep<T>() where T : IApplicationInitializationStep
            => builder.RemoveInitializationStep(t => t is T);

        public INOFAppBuilder TryAddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep initializationStep, params Type[] allInterfaces)
            where TStep : IApplicationInitializationStep
            => (INOFAppBuilder)((IServiceRegistrationContext)builder).TryAddInitializationStep(initializationStep, allInterfaces);

        public INOFAppBuilder TryAddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep initializationStep)
            where TStep : IApplicationInitializationStep
            => (INOFAppBuilder)((IServiceRegistrationContext)builder).TryAddInitializationStep(initializationStep);

        public INOFAppBuilder TryAddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] T>() where T : IApplicationInitializationStep, new()
            => (INOFAppBuilder)((IServiceRegistrationContext)builder).TryAddInitializationStep<T>();
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
