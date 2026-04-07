using Microsoft.Extensions.DependencyInjection.Extensions;
using NOF.Contract;
using System.Diagnostics.CodeAnalysis;

namespace NOF.Hosting;

public interface IOutboundMiddlewareStep : IServiceRegistrationStep
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
