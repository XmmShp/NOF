using Microsoft.Extensions.DependencyInjection;
using NOF.Infrastructure.Core;

namespace NOF.Infrastructure.MassTransit;

public readonly struct MassTransitSelector
{
    public INOFAppBuilder Builder { get; }
    public MassTransitSelector(INOFAppBuilder builder)
    {
        Builder = builder;
    }

    public MassTransitSelector AddRequestHandleNode(Type nodeType)
    {
        Builder.AddInitializationStep((_, app) =>
        {
            app.Services.GetRequiredService<IRequestHandleNodeRegistry>().Registry.AddFirst(nodeType);
            return Task.CompletedTask;
        });
        return this;
    }
}
