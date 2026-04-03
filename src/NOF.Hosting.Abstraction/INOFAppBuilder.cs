using Microsoft.Extensions.Hosting;
using NOF.Annotation;
using System.Reflection;

namespace NOF.Hosting;

/// <summary>
/// Represents a configurable application host builder for the NOF framework,
/// providing a fluent API to customize service registration, application startup,
/// metadata, and integration with infrastructure.
/// <para>
/// This is the full builder interface available before <c>BuildAsync</c> is called.
/// During step execution, narrower context interfaces are used to prevent invalid operations:
/// <list type="bullet">
///   <item><see cref="IServiceRegistrationContext"/> used by <see cref="IServiceRegistrationStep"/>; cannot add registration steps.</item>
///   <item><see cref="IHostApplicationBuilder"/> used by <see cref="IApplicationInitializationStep"/>; cannot add any steps.</item>
/// </list>
/// </para>
/// </summary>
public interface INOFAppBuilder : IServiceRegistrationContext
{
    /// <summary>
    /// Adds an application part assembly and executes its assembly initializers.
    /// </summary>
    INOFAppBuilder AddApplicationPart(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var attribute in assembly.GetCustomAttributes<AssemblyInitializeAttribute>())
        {
            attribute.InitializeMethod();
        }

        return this;
    }

    /// <summary>
    /// Registers a service configuration delegate that runs during DI container setup.
    /// Use this to add services required by your application or modules.
    /// </summary>
    /// <param name="registrationStep">The service configurator to register. Must not be null.</param>
    /// <returns>The current builder instance to allow method chaining.</returns>
    INOFAppBuilder AddRegistrationStep(IServiceRegistrationStep registrationStep);

    /// <summary>
    /// Removes all previously registered service configurators that satisfy the given condition.
    /// </summary>
    /// <param name="predicate">A predicate used to identify which service configurators to remove.</param>
    /// <returns>The current builder instance to allow method chaining.</returns>
    INOFAppBuilder RemoveRegistrationStep(Predicate<IServiceRegistrationStep> predicate);

    /// <summary>
    /// Removes all registered service configurations of the specified type <typeparamref name="T"/>.
    /// </summary>
    INOFAppBuilder RemoveRegistrationStep<T>() where T : IServiceRegistrationStep
        => RemoveRegistrationStep(t => t is T);

    /// <summary>
    /// Adds the registration step only if a step of the same runtime type has not been registered.
    /// </summary>
    INOFAppBuilder TryAddRegistrationStep(IServiceRegistrationStep registrationStep)
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

        return AddRegistrationStep(registrationStep);
    }

    /// <summary>
    /// Adds the registration step type only if it has not been registered.
    /// </summary>
    INOFAppBuilder TryAddRegistrationStep<T>() where T : IServiceRegistrationStep, new()
        => TryAddRegistrationStep(new T());

    IServiceRegistrationContext IServiceRegistrationContext.AddInitializationStep(IApplicationInitializationStep initializationStep)
        => AddInitializationStep(initializationStep);

    IServiceRegistrationContext IServiceRegistrationContext.RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate)
        => RemoveInitializationStep(predicate);

    /// <inheritdoc cref="IServiceRegistrationContext.AddInitializationStep(IApplicationInitializationStep)"/>
    new INOFAppBuilder AddInitializationStep(IApplicationInitializationStep initializationStep);

    /// <inheritdoc cref="IServiceRegistrationContext.RemoveInitializationStep(Predicate{IApplicationInitializationStep})"/>
    new INOFAppBuilder RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate);

    /// <summary>
    /// Removes all registered application configurations of the specified type <typeparamref name="T"/>.
    /// </summary>
    new INOFAppBuilder RemoveInitializationStep<T>() where T : IApplicationInitializationStep
        => RemoveInitializationStep(t => t is T);

    /// <summary>
    /// Adds the initialization step only if a step of the same runtime type has not been registered.
    /// </summary>
    new INOFAppBuilder TryAddInitializationStep(IApplicationInitializationStep initializationStep)
        => (INOFAppBuilder)((IServiceRegistrationContext)this).TryAddInitializationStep(initializationStep);

    /// <summary>
    /// Adds the initialization step type only if it has not been registered.
    /// </summary>
    new INOFAppBuilder TryAddInitializationStep<T>() where T : IApplicationInitializationStep, new()
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
