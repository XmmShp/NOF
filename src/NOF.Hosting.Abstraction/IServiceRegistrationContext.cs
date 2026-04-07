using Microsoft.Extensions.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

/// <summary>
/// Context available during service registration steps.
/// </summary>
public interface IServiceRegistrationContext : IHostApplicationBuilder
{
    IServiceRegistrationContext AddInitializationStep(IApplicationInitializationStep initializationStep, params Type[] allInterfaces);

    IServiceRegistrationContext AddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep initializationStep)
        where TStep : IApplicationInitializationStep
        => AddInitializationStep(initializationStep, [.. DependencyNode<IApplicationInitializationStep>.CollectRelatedTypes<TStep>()]);

    IServiceRegistrationContext RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate);

    IServiceRegistrationContext RemoveInitializationStep<T>() where T : IApplicationInitializationStep
        => RemoveInitializationStep(t => t is T);

    IServiceRegistrationContext TryAddInitializationStep(IApplicationInitializationStep initializationStep, params Type[] allInterfaces)
    {
        ArgumentNullException.ThrowIfNull(initializationStep);
        var exists = false;
        RemoveInitializationStep(existing =>
        {
            if (existing.GetType() == initializationStep.GetType())
            {
                exists = true;
            }
            return false;
        });

        if (exists)
        {
            return this;
        }

        return AddInitializationStep(initializationStep, allInterfaces);
    }

    IServiceRegistrationContext TryAddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep initializationStep)
        where TStep : IApplicationInitializationStep
        => TryAddInitializationStep(initializationStep, [.. DependencyNode<IApplicationInitializationStep>.CollectRelatedTypes<TStep>()]);

    IServiceRegistrationContext TryAddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] T>() where T : IApplicationInitializationStep, new()
        => TryAddInitializationStep(new T());
}
