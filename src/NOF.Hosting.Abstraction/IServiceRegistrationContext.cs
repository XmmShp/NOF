using Microsoft.Extensions.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

/// <summary>
/// Context available during service registration steps.
/// </summary>
public interface IServiceRegistrationContext : IHostApplicationBuilder
{
    IServiceRegistrationContext AddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep initializationStep, params Type[] allInterfaces)
        where TStep : IApplicationInitializationStep;

    IServiceRegistrationContext RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate);
}

public static partial class NOFHostingExtensions
{
    extension(IServiceRegistrationContext context)
    {
        public IServiceRegistrationContext AddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep initializationStep)
            where TStep : IApplicationInitializationStep
            => context.AddInitializationStep(initializationStep, [.. DependencyNode.CollectRelatedTypes<TStep>()]);

        public IServiceRegistrationContext RemoveInitializationStep<T>() where T : IApplicationInitializationStep
            => context.RemoveInitializationStep(t => t is T);

        public IServiceRegistrationContext TryAddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep initializationStep, params Type[] allInterfaces)
            where TStep : IApplicationInitializationStep
        {
            ArgumentNullException.ThrowIfNull(initializationStep);
            var exists = false;
            context.RemoveInitializationStep(existing =>
            {
                if (existing.GetType() == initializationStep.GetType())
                {
                    exists = true;
                }
                return false;
            });

            if (exists)
            {
                return context;
            }

            return context.AddInitializationStep(initializationStep, allInterfaces);
        }

        public IServiceRegistrationContext TryAddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(TStep initializationStep)
            where TStep : IApplicationInitializationStep
            => context.TryAddInitializationStep(initializationStep, [.. DependencyNode.CollectRelatedTypes<TStep>()]);

        public IServiceRegistrationContext TryAddInitializationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] T>() where T : IApplicationInitializationStep, new()
            => context.TryAddInitializationStep(new T());
    }
}
