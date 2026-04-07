using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Application;
using NOF.Contract;
using NOF.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Infrastructure;

public interface IBaseSettingsServiceRegistrationStep : IServiceRegistrationStep;

public interface IDependentServiceRegistrationStep : IServiceRegistrationStep, IAfter<IBaseSettingsServiceRegistrationStep>;

public interface IInboundMiddlewareStep : IServiceRegistrationStep, IAfter<IDependentServiceRegistrationStep>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type MiddlewareType { get; }

    ValueTask IServiceRegistrationStep.ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.TryAddScoped(MiddlewareType);
        builder.Services.GetOrAddSingleton<InboundPipelineTypes>().Add(MiddlewareType);
        return ValueTask.CompletedTask;
    }
}

public sealed class InboundMiddlewareRegistrationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>
    : IInboundMiddlewareStep
    where TMiddleware : class, IInboundMiddleware
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type MiddlewareType => typeof(TMiddleware);
}

public interface IOutboundMiddlewareStep : IServiceRegistrationStep, IAfter<IDependentServiceRegistrationStep>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type MiddlewareType { get; }

    ValueTask IServiceRegistrationStep.ExecuteAsync(IServiceRegistrationContext builder)
    {
        builder.Services.TryAddScoped(MiddlewareType);
        builder.Services.GetOrAddSingleton<OutboundPipelineTypes>().Add(MiddlewareType);
        return ValueTask.CompletedTask;
    }
}

public sealed class OutboundMiddlewareRegistrationStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>
    : IOutboundMiddlewareStep
    where TMiddleware : class, IOutboundMiddleware
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type MiddlewareType => typeof(TMiddleware);
}

public interface IDataSeedInitializationStep : IApplicationInitializationStep;

public interface IObservabilityInitializationStep : IApplicationInitializationStep, IAfter<IDataSeedInitializationStep>;

public interface ISecurityInitializationStep : IApplicationInitializationStep, IAfter<IObservabilityInitializationStep>;

public interface IResponseFormattingInitializationStep : IApplicationInitializationStep, IAfter<ISecurityInitializationStep>;

public interface IAuthenticationInitializationStep : IApplicationInitializationStep, IAfter<IResponseFormattingInitializationStep>;

public interface IBusinessLogicInitializationStep : IApplicationInitializationStep, IAfter<IAuthenticationInitializationStep>;

public interface IEndpointInitializationStep : IApplicationInitializationStep, IAfter<IBusinessLogicInitializationStep>;
