using Microsoft.Extensions.Hosting;
using NOF.Annotation;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NOF.Hosting;

public interface INOFAppBuilder : IServiceRegistrationContext
{
    INOFAppBuilder AddApplicationPart(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var attribute in assembly.GetCustomAttributes<AssemblyInitializeAttribute>())
        {
            attribute.InitializeMethod();
        }

        return this;
    }

    INOFAppBuilder AddRegistrationStep(IServiceRegistrationStep registrationStep, params Type[] allInterfaces);

    INOFAppBuilder AddRegistrationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep registrationStep)
        where TStep : IServiceRegistrationStep
        => AddRegistrationStep(registrationStep, [.. DependencyNode<IServiceRegistrationStep>.CollectRelatedTypes<TStep>()]);

    INOFAppBuilder RemoveRegistrationStep(Predicate<IServiceRegistrationStep> predicate);

    INOFAppBuilder RemoveRegistrationStep<T>() where T : IServiceRegistrationStep
        => RemoveRegistrationStep(t => t is T);

    INOFAppBuilder TryAddRegistrationStep(IServiceRegistrationStep registrationStep, params Type[] allInterfaces)
    {
        ArgumentNullException.ThrowIfNull(registrationStep);
        var exists = false;
        RemoveRegistrationStep(existing =>
        {
            if (existing.GetType() == registrationStep.GetType())
            {
                exists = true;
            }
            return false;
        });

        if (exists)
        {
            return this;
        }

        return AddRegistrationStep(registrationStep, allInterfaces);
    }

    INOFAppBuilder TryAddRegistrationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep registrationStep)
        where TStep : IServiceRegistrationStep
        => TryAddRegistrationStep(registrationStep, [.. DependencyNode<IServiceRegistrationStep>.CollectRelatedTypes<TStep>()]);

    INOFAppBuilder TryAddRegistrationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] T>() where T : IServiceRegistrationStep, new()
        => TryAddRegistrationStep(new T());

    IServiceRegistrationContext IServiceRegistrationContext.AddInitializationStep(IApplicationInitializationStep initializationStep, params Type[] allInterfaces)
        => AddInitializationStep(initializationStep, allInterfaces);

    IServiceRegistrationContext IServiceRegistrationContext.RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate)
        => RemoveInitializationStep(predicate);

    new INOFAppBuilder AddInitializationStep(IApplicationInitializationStep initializationStep, params Type[] allInterfaces);

    new INOFAppBuilder RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate);

    new INOFAppBuilder RemoveInitializationStep<T>() where T : IApplicationInitializationStep
        => RemoveInitializationStep(t => t is T);

    new INOFAppBuilder TryAddInitializationStep(IApplicationInitializationStep initializationStep, params Type[] allInterfaces)
        => (INOFAppBuilder)((IServiceRegistrationContext)this).TryAddInitializationStep(initializationStep, allInterfaces);

    new INOFAppBuilder TryAddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep initializationStep)
        where TStep : IApplicationInitializationStep
        => (INOFAppBuilder)((IServiceRegistrationContext)this).TryAddInitializationStep(initializationStep);

    new INOFAppBuilder TryAddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] T>() where T : IApplicationInitializationStep, new()
        => (INOFAppBuilder)((IServiceRegistrationContext)this).TryAddInitializationStep<T>();
}

public static partial class NOFAppBuilderExtensions
{
    extension(INOFAppBuilder builder)
    {
        public INOFAppBuilder AddApplicationPart(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            foreach (var attribute in assembly.GetCustomAttributes<AssemblyInitializeAttribute>())
            {
                attribute.InitializeMethod();
            }

            return builder;
        }
    }
}
