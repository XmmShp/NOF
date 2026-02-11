using Microsoft.Extensions.DependencyInjection;

namespace NOF;

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
