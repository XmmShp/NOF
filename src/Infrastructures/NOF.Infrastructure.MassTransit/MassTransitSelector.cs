using NOF.Infrastructure.Core;

namespace NOF.Infrastructure.MassTransit;

public readonly struct MassTransitSelector
{
    public INOFAppBuilder Builder { get; }
    public MassTransitSelector(INOFAppBuilder builder)
    {
        Builder = builder;
    }
}
