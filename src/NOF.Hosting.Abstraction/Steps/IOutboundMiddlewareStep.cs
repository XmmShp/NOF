using Microsoft.Extensions.DependencyInjection.Extensions;
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

public interface IOutboundMiddlewareStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf> : IOutboundMiddlewareStep, IStep<TSelf>
    where TSelf : IOutboundMiddlewareStep<TSelf>;

public interface IOutboundMiddlewareStep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TSelf, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>
    : IOutboundMiddlewareStep<TSelf>
    where TSelf : IOutboundMiddlewareStep<TSelf, TMiddleware>
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type IOutboundMiddlewareStep.MiddlewareType => typeof(TMiddleware);
}
